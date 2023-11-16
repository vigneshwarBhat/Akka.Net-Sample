using Akka.Actor;
using Akka.Cluster.Infra;
using Akka.Cluster.Infra.Events;
using Akka.Cluster.Infra.Events.Persistence;
using Akka.Event;
using Akka.Persistence;
using Akka.Persistence.Extras;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using System.Diagnostics;

namespace CartItemProcessor_1.Actor
{
    public class CartItemPersistenceActor : ReceivePersistentActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();
        public const int SnapshotInterval = 100;
        private readonly CartData _cartEngine;
        private static readonly ActivitySource ActivitySource = new(Instrumentation.ActivitySourceName);
        private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
        public CartItemPersistenceActor(string persistenceId):this(persistenceId, null)
        {
            
        }

        public CartItemPersistenceActor(string persistenceId, CartData? cartEngine)
        {
            var items = persistenceId.Split('|');
            _cartEngine = cartEngine ?? new CartData();
            PersistenceId = items[0];
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

            Command<CreateCartItemRequest>(cart =>
            {

                var parentContext = Propagator.Extract(default, cart, InstrumentationHelper.ExtractTraceContextFromBasicProperties);
                Baggage.Current = parentContext.Baggage;
                using var activity = ActivitySource.StartActivity(nameof(Commands), ActivityKind.Internal, parentContext.ActivityContext);
                activity?.SetTag("cartID", cart.CartId);
                activity?.SetTag("cartItemID", cart.CartItemId);
                activity?.AddEvent(new ActivityEvent("Creating cart item"));
                //Business logic.
                //Thread.Sleep(1000);
                PersistsAll(cart);
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
            if (LastSequenceNr !=0 && LastSequenceNr % SnapshotInterval == 0)
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
