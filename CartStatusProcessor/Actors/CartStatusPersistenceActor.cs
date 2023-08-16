using Akka.Actor;
using Akka.Cluster.Infra;
using Akka.Cluster.Infra.Events;
using Akka.Cluster.Infra.Events.Persistence;
using Akka.Event;
using Akka.Persistence;


namespace CartStatusProcessor
{
    public sealed class CartStatusPersistenceActor : ReceivePersistentActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();
        public const int SnapshotInterval = 100;
        private readonly CartData _cartData;
        public override string PersistenceId { get; }
        public CartStatusPersistenceActor(string persistenceId) : this(persistenceId, null)
        {

        }

        public CartStatusPersistenceActor(string persistenceId, CartData? cartEngine)
        {
            _cartData = cartEngine ?? new CartData();
            PersistenceId = persistenceId;
            Recovers();
            Commands();
        }

        private void Commands()
        {

            Command<GetCartStatus>(cartStatusReq =>
            {
                var cart = GetCartStatus(cartStatusReq);
                Sender.Tell(cart);
            });
        }

        private void Recovers()
        {
            Recover<SnapshotOffer>(offer =>
            {
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
            });

            Recover<CartEvent>(cart =>
            {
                _cartData.CartEvents.Add(cart);
                _log.Info($"cart journal recovery completed.");
            });

            Recover<CartItemEvent>(b =>
            {
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
}