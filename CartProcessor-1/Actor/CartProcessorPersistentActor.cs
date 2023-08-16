using Akka.Actor;
using Akka.Cluster.Infra;
using Akka.Cluster.Infra.Events;
using Akka.Cluster.Infra.Events.Persistence;
using Akka.Event;
using Akka.Hosting;
using Akka.Persistence;
using Akka.Persistence.Extras;

public class CartProcessorPersistentActor : ReceivePersistentActor
{
    private string _persistenceId;
    public override string PersistenceId => _persistenceId;
    private IActorRef _shardCartItemActor;
    private readonly CartData _cartEngine;
    public const int SnapshotInterval = 100;

    private readonly ILoggingAdapter _log = Context.GetLogger();
    public CartProcessorPersistentActor(IActorRegistry _actorRegistry, string cartId) : this(_actorRegistry, cartId, null) { }

    public CartProcessorPersistentActor(IActorRegistry actorRegistry, string cartId, CartData cartEngine)
    {
        _persistenceId = cartId;
        _shardCartItemActor = actorRegistry.Get<IShardProxyActor>();
        _cartEngine = cartEngine ?? new CartData();
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

        Command<GetCartStatus>(cartReq =>
        {
            var result = _shardCartItemActor.Ask<List<CartItemEvent>>(cartReq);
            var status = GetCartStatus(cartReq);
            if (status == null)
                Sender.Tell(new CartJournal());
            Sender.Tell(status);
        });

        Command<ConfirmableMessage<CreateCartItemRequest>>(a =>
        {
            var confirmation = new Confirmation(a.ConfirmationId, PersistenceId);
            var createCartReq = a.Message;
            _cartEngine.CartItemEvents.Add(new CartItemEvent
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
            _cartEngine.CartItemEvents.Add(new CartItemEvent
            {
                CartId = cartReq.CartId,
                CartItemId = cartReq.CartItemId,
                Quantity = 1,
                Status = "InProgress"
            });
            _shardCartItemActor.Tell(cartReq);
        });


        Command<SaveSnapshotSuccess>(s =>
        {
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
        Persist(createCartReq, (evt) =>
        {
            _cartEngine.CartEvents.Add(evt);
        });
        _log.Info($"Cart with cart Id:{createCartReq.CartId} is saved in persistence");

        if (LastSequenceNr != 0 && LastSequenceNr % SnapshotInterval == 0)
        {
            SaveSnapshot(_cartEngine.CartEvents);
            _log.Info($"Cart snap shot is created.");
        }
    }

    private void Recovers()
    {
        Recover<SnapshotOffer>(offer =>
            {
                if (offer.Snapshot is List<CartEvent> cartList)
                {
                    _cartEngine.CartEvents.AddRange(cartList);
                    _log.Info($"Cart snapshot recovery completed.");
                }
                if (offer.Snapshot is List<CartItemEvent> carts)
                {
                    _cartEngine.CartItemEvents.AddRange(carts);
                    _log.Info($"Cart item snapshot recovery completed.");
                }
            });

        Recover<CartEvent>(cart =>
        {
            _cartEngine.CartEvents.Add(cart);
            _log.Info($"cart journal recovery completed.");
        });

        Recover<CartItemEvent>(b =>
        {
            _cartEngine.CartItemEvents.Add(b);
            _log.Info($"cart item journal recovery completed.");
        });
    }

    private CartJournal? GetCartStatus(GetCartStatus getCartStatus)
    {
        var cartData = _cartEngine.CartEvents.OrderByDescending(evt => evt.TimePlaced).FirstOrDefault();
        var cartItems = _cartEngine.CartItemEvents
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