using Akka.Actor;
using Akka.Cluster.Infra;
using Akka.Cluster.Infra.Events;
using Akka.Cluster.Infra.Events.Persistence;
using Akka.Event;
using Akka.Persistence;
using Akka.Persistence.Extras;
using Conga.Platform.DocumentManagement.Client;
using Conga.Platform.DocumentManagement.Client.Models.Request;
using Conga.Platform.DocumentManagement.Client.Models.Response;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using System.Diagnostics;
using System.Text.Json;

namespace CartItemProcessor_1.Actor
{
    public class CartItemPersistenceActor : ReceivePersistentActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();
        public const int SnapshotInterval = 100;
        private readonly IDocumentManagementClient _documentManagementClient;
        private readonly CartData _cartEngine;
        private static readonly ActivitySource ActivitySource = new(Instrumentation.ActivitySourceName);
        private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
        public CartItemPersistenceActor(string persistenceId, IDocumentManagementClient documentManagementClient) : this(persistenceId, null, documentManagementClient)
        {

        }

        public CartItemPersistenceActor(string persistenceId, CartData? cartEngine, IDocumentManagementClient documentManagementClient)
        {
            var items = persistenceId.Split('|');
            _cartEngine = cartEngine ?? new CartData();
            PersistenceId = items[0];
            _documentManagementClient = documentManagementClient;
            Recovers();
            Commands();
        }

        public override string PersistenceId { get; }

        private void Commands()
        {
            Command<ConfirmableMessage<CreateCartItemRequest>>(b =>
            {
                // For the sake of efficiency -update orderbook and then persist all events
                var confirmation = new Confirmation(b.ConfirmationId, PersistenceId);
                var cart = b.Message;
                PersistsAll(cart);
            });

            CommandAsync<CreateCartItemRequest>(async cart =>
            {

                var parentContext = Propagator.Extract(default, cart, InstrumentationHelper.ExtractTraceContextFromBasicProperties);
                Baggage.Current = parentContext.Baggage;
                using (var activity = ActivitySource.StartActivity(nameof(Commands), ActivityKind.Internal, parentContext.ActivityContext))
                {
                    activity?.SetTag("cartID", cart.CartId);
                    activity?.SetTag("cartItemID", cart.CartItemId);
                    activity?.AddEvent(new ActivityEvent("Creating cart item"));

                    var document = await JsonHelper.ReadAsync<Document>("DocumentId1.json");
                    var randomDocIds = document.DocumentIds.OrderBy(x => Guid.NewGuid()).ToList().Take(5000);
                    var iterationCount = Math.Ceiling((decimal)5000 / 100);
                    var taskList = new List<Task<DocumentGetManyResponse>>();
                    using (var activity1 = ActivitySource.StartActivity("Getting document meta data and blob", ActivityKind.Internal, activity.Context))
                    {
                        activity1?.AddEvent(new ActivityEvent($"started sending requests to doc management at {DateTime.Now}."));
                        for (var i = 0; i < iterationCount; i++)
                        {
                            activity1?.AddEvent(new ActivityEvent($"Document management call number {i} at {DateTime.Now}"));
                            var docIds = randomDocIds.Skip(i * 100).Take(100).ToList();
                            activity1?.SetTag("ids", docIds);
                            taskList.Add(_documentManagementClient.GetManyAsync(new DocumentGetManyRequest
                            {
                                DocumentIds = docIds
                            }));
                        }
                        var result = await Task.WhenAll(taskList);
                        activity1?.AddEvent(new ActivityEvent($"Got a response for all 50 calls for doc management at {DateTime.Now}."));
                    }
                    //Business logic.
                    //Thread.Sleep(1000);
                    PersistsAll(cart);
                }

            });
        }

        private void PersistsAll(CreateCartItemRequest createCartItemRequest)
        {
            using (var activity = ActivitySource.StartActivity(nameof(PersistsAll), ActivityKind.Internal))
            {
                activity?.SetTag("cartID", createCartItemRequest.CartId);
                activity?.SetTag("cartItemID", createCartItemRequest.CartItemId);
                activity?.AddEvent(new ActivityEvent("persisting cart item"));

                var cartItemEvent = new CartItemEvent
                {
                    CartId = createCartItemRequest.CartId,
                    CartItemId = createCartItemRequest.CartItemId,
                    Quantity = 1,
                    Status = "Completed"
                };
                Persist(cartItemEvent, (evt) =>
                   {
                       _cartEngine.CartItemEvents.Add(evt);
                       _log.Info($"Cart item: {createCartItemRequest.CartItemId} belonging to cart {createCartItemRequest.CartId} got persisted.");
                   });
            }
            if (LastSequenceNr != 0 && LastSequenceNr % SnapshotInterval == 0)
            {
                using (var activity = ActivitySource.StartActivity(nameof(PersistsAll), ActivityKind.Internal))
                {
                    activity?.SetTag("cartID", createCartItemRequest.CartId);
                    activity?.SetTag("cartItemID", createCartItemRequest.CartItemId);
                    activity?.AddEvent(new ActivityEvent("saving snapshot of cart item"));
                    SaveSnapshot(_cartEngine.CartItemEvents);
                    _log.Info($"Cart snap shot is created.");
                }
            }

        }

        private void Recovers()
        {
            Recover<SnapshotOffer>(offer =>
            {
                using var activity = ActivitySource.StartActivity(nameof(Recovers), ActivityKind.Internal);
                activity?.AddEvent(new ActivityEvent("Recovering cart item snapshot."));
                if (offer.Snapshot is List<CartItemEvent> carts)
                {
                    _cartEngine.CartItemEvents.AddRange(carts);
                    _log.Info($"Snapshot recovery completed.");
                }
            });

            Recover<CartItemEvent>(b =>
            {
                using var activity = ActivitySource.StartActivity(nameof(Recovers), ActivityKind.Internal);
                activity?.AddEvent(new ActivityEvent("Recovering cart item journal."));
                _cartEngine.CartItemEvents.Add(b);
                _log.Info($"cart journal recovery completed.");
            });

        }


    }
}
