using Akka.Actor;
using Akka.Cluster.Infra;
using Akka.Cluster.Infra.Events;
using Akka.Cluster.Infra.Events.Persistence;
using Akka.Event;
using Akka.Persistence;
using Akka.Persistence.Extras;

namespace CartItemProcessor_1.Actor
{
    public class CartItemPersistenceActor : ReceivePersistentActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();
        public const int SnapshotInterval = 100;
        private readonly CartData _cartEngine;
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
                //Business logic.
                Thread.Sleep(1000);
                PersistsAll(cart);
            });
        }

        private void PersistsAll(CreateCartItemRequest createCartItemRequest)
        {
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
            if (LastSequenceNr !=0 && LastSequenceNr % SnapshotInterval == 0)
            {
                SaveSnapshot(_cartEngine.CartItemEvents);
                _log.Info($"Cart snap shot is created.");
            }

        }

        private void Recovers()
        {
            Recover<SnapshotOffer>(offer =>
            {
                if (offer.Snapshot is List<CartItemEvent> carts)
                {
                    _cartEngine.CartItemEvents.AddRange(carts);
                    _log.Info($"Snapshot recovery completed.");
                }
            });

            Recover<CartItemEvent>(b =>
            {
                _cartEngine.CartItemEvents.Add(b);
                _log.Info($"cart journal recovery completed.");
            });

        }

    }
}
