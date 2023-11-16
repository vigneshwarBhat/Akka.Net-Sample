using Akka.Actor;
using Akka.Cluster.Infra;
using Akka.Cluster.Infra.Events;
using Akka.Cluster.Infra.Events.Persistence;
using Akka.Event;
using Akka.Hosting;
using Akka.Persistence;
using Akka.Persistence.Extras;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;
using System.Diagnostics;

public class CartProcessorPersistentActor : ReceivePersistentActor
{
    private string _persistenceId;
    public override string PersistenceId => _persistenceId;
    private IActorRef _shardCartItemActor;
    private readonly CartData _cartData;
    public const int SnapshotInterval = 100;
    private static readonly ActivitySource ActivitySource = new(Instrumentation.ActivitySourceName);
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    public CartProcessorPersistentActor(IActorRegistry _actorRegistry, string cartId) : this(_actorRegistry, cartId, null) { }

    public CartProcessorPersistentActor(IActorRegistry actorRegistry, string cartId, CartData cartData)
    {
        _persistenceId = cartId;
        _shardCartItemActor = actorRegistry.Get<IShardProxyActor>();
        _cartData = cartData ?? new CartData();
        Recovers();
        Commands();
    }

    private void Commands()
    {
        Command<ConfirmableMessage<CreateCartRequest>>(a =>
        {
            var confirmation = new Confirmation(a.ConfirmationId, PersistenceId);
            var createCartReq = a.Message;
            var createcartEvent = new CartEvent
            {
                CartId = createCartReq.CartId,
                Status = "Inprogress"
            };
            ProcessAsk(createcartEvent, confirmation);
        });

        Command<CreateCartRequest>(cartReq =>
        {
            var parentContext = Propagator.Extract(default, cartReq, InstrumentationHelper.ExtractTraceContextFromBasicProperties);
            Baggage.Current = parentContext.Baggage;
            using var activity = ActivitySource.StartActivity(nameof(CartProcessorPersistentActor), ActivityKind.Internal, parentContext.ActivityContext);
            activity?.AddEvent(new ActivityEvent("Creating cart"));
            var createcartEvent = new CartEvent
            {
                CartId = cartReq.CartId,
                Status = "Inprogress"
            };
            ProcessAsk(createcartEvent, null);
        });

        Command<ConfirmableMessage<GetCartStatus>>(a =>
        {
            var confirmation = new Confirmation(a.ConfirmationId, PersistenceId);
            var cartStatusReq = a.Message;
            var status = GetCartStatus(cartStatusReq);
            if (status == null)
                Sender.Tell(new CartJournal());
            Sender.Tell(status);
        });

        Command<GetCartStatus>(async cartReq =>
        {
            var parentContext = Propagator.Extract(default, cartReq, InstrumentationHelper.ExtractTraceContextFromBasicProperties);
            Baggage.Current = parentContext.Baggage;
            using var activity = ActivitySource.StartActivity(nameof(CartProcessorPersistentActor), ActivityKind.Internal, parentContext.ActivityContext);
            activity?.SetTag("CartId", cartReq.CartId);
            activity?.AddEvent(new ActivityEvent("Getting cart status started"));

            InstrumentationHelper.AddActivityToRequest(activity, cartReq, "POST api/{cartId}/items", nameof(CartProcessorPersistentActor));
            var result = await _shardCartItemActor.Ask<List<CartItemEvent>>(cartReq);
            var status = GetCartStatus(cartReq);
            activity?.AddEvent(new ActivityEvent("Getting cart status completed"));
            if (status == null)
                Sender.Tell(new CartJournal());
            Sender.Tell(status);
        });

        Command<ConfirmableMessage<CreateCartItemRequest>>(a =>
        {
            var confirmation = new Confirmation(a.ConfirmationId, PersistenceId);
            var createCartReq = a.Message;
            _cartData.CartItemEvents.Add(new CartItemEvent
            {
                CartId = createCartReq.CartId,
                CartItemId = createCartReq.CartItemId,
                Quantity = 1,
                Status = "InProgress"
            });
            _shardCartItemActor.Tell(createCartReq);
        });

        Command<CreateCartItemRequest>(cartReq =>
        {
            var parentContext = Propagator.Extract(default, cartReq, InstrumentationHelper.ExtractTraceContextFromBasicProperties);
            Baggage.Current = parentContext.Baggage;
            using var activity = ActivitySource.StartActivity(nameof(CartProcessorPersistentActor), ActivityKind.Internal, parentContext.ActivityContext);
            activity?.SetTag("CartId", cartReq.CartId);
            activity?.SetTag("CartItemId", cartReq.CartItemId);
            activity?.AddEvent(new ActivityEvent("Creating cart item"));
            _cartData.CartItemEvents.Add(new CartItemEvent
            {
                CartId = cartReq.CartId,
                CartItemId = cartReq.CartItemId,
                Quantity = 1,
                Status = "InProgress"
            });
            InstrumentationHelper.AddActivityToRequest(activity, cartReq, "POST api/{cartId}/items", nameof(CartProcessorPersistentActor));
            _shardCartItemActor.Tell(cartReq);
        });


        Command<SaveSnapshotSuccess>(s =>
        {
            using var activity = ActivitySource.StartActivity(nameof(Commands), ActivityKind.Internal);
            activity?.AddEvent(new ActivityEvent("Callback after snapshot got saved and starting the deletion of journal."));
            // soft-delete the journal up until the sequence # at
            // which the snapshot was taken
            DeleteSnapshots(new SnapshotSelectionCriteria(s.Metadata.SequenceNr - 1));
            DeleteMessages(s.Metadata.SequenceNr);
            _log.Info($"Deleted all the persistent messages and snapshots below sequence number {s.Metadata.SequenceNr}");
        });
        Command<SaveSnapshotFailure>(failure =>
        {
            // handle snapshot save failure...
            _log.Warning($"Failed to store snapshot: {failure.Cause} ");
        });
    }

    private void ProcessAsk(CartEvent createCartReq, Confirmation? confirmation)
    {
        using (var activity = ActivitySource.StartActivity(nameof(ProcessAsk), ActivityKind.Internal))
        {
            activity?.AddEvent(new ActivityEvent("Persisting the cart"));
            Persist(createCartReq, (evt) =>
            {
                _cartData.CartEvents.Add(evt);
            });
            _log.Info($"Cart with cart Id:{createCartReq.CartId} is saved in persistence");


            if (LastSequenceNr != 0 && LastSequenceNr % SnapshotInterval == 0)
            {
                using var newactivity = ActivitySource.StartActivity(nameof(ProcessAsk), ActivityKind.Internal);
                newactivity?.AddEvent(new ActivityEvent("Persisting the cart snapshot"));
                SaveSnapshot(_cartData.CartEvents);
                _log.Info($"Cart snap shot is created.");
            }
        }
    }

    private void Recovers()
    {
        Recover<SnapshotOffer>(offer =>
            {
                using var activity = ActivitySource.StartActivity(nameof(Recovers), ActivityKind.Internal);
                activity?.AddEvent(new ActivityEvent("Getting cart and cart items from snapshot store started."));
                if (offer.Snapshot is List<CartEvent> cartList)
                {
                    _cartData.CartEvents.AddRange(cartList);
                    _log.Info($"Cart snapshot recovery completed.");
                }
                if (offer.Snapshot is List<CartItemEvent> carts)
                {
                    _cartData.CartItemEvents.AddRange(carts);
                    _log.Info($"Cart item snapshot recovery completed.");
                }
                activity?.AddEvent(new ActivityEvent("Getting cart and cart items from snapshot store completed."));
            });

        Recover<CartEvent>(cart =>
        {
            using var activity = ActivitySource.StartActivity(nameof(Recovers), ActivityKind.Internal);
            activity?.AddEvent(new ActivityEvent("Recovering carts from cart journal"));
            _cartData.CartEvents.Add(cart);
            _log.Info($"cart journal recovery completed.");
        });

        Recover<CartItemEvent>(b =>
        {
            using var activity = ActivitySource.StartActivity(nameof(Recovers), ActivityKind.Internal);
            activity?.AddEvent(new ActivityEvent("Recovering cart items from cart item journal"));
            _cartData.CartItemEvents.Add(b);
            _log.Info($"cart item journal recovery completed.");
        });
    }

    private CartJournal? GetCartStatus(GetCartStatus getCartStatus)
    {
        var cartData = _cartData.CartEvents.OrderByDescending(evt => evt.TimePlaced).FirstOrDefault();
        var cartItems = _cartData.CartItemEvents
                      .Where(item => item.CartId == getCartStatus.CartId)
                      .OrderByDescending(x => x.TimePlaced)
                      .DistinctBy(x => x.CartItemId)
                      .SelectMany(evt => new List<CartItemJournal>
                      {
                              new CartItemJournal
                              {
                                  CartItemId = evt.CartItemId,
                                  Status = evt.Status,
                                  Quantity = evt.Quantity
                              }
                      }).ToList();

        if (cartData != null)
        {
            var cart = new CartJournal
            {
                CartId = cartData.CartId,
                CartStatus = cartData.Status,
                CartItemSnapshots = cartItems
            };

            return cart;
        }
        return null;
    }
}