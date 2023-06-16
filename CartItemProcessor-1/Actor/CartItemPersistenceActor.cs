using Akka.Cluster.Infra;
using Akka.Cluster.Infra.Events;
using Akka.Persistence;
using Akka.Persistence.Extras;
using Akka.Streams.Implementation.Fusing;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CartItemProcessor_1.Actor
{
    public class CartItemPersistenceActor : ReceivePersistentActor
    {
        private CartJournal _cartjournal;
        public CartItemPersistenceActor(string cartId, string CartItemId)
        {
            PersistenceId = cartId;
            _cartjournal = new CartJournal();
            Recovers();
            Commands();
        }


        public override string PersistenceId { get; }

        private void Commands()
        {
            Command<ConfirmableMessage<CreateCartRequest>>(a =>
            {
                // For the sake of efficiency - update orderbook and then persist all events
                var confirmation = new Confirmation(a.ConfirmationId, PersistenceId);
                var ask = a.Message;
                //ProcessAsk(ask, confirmation);
            });

            Command<CreateCartRequest>(a =>
            {
                //ProcessAsk(a, null);
            });

            Command<ConfirmableMessage<CreateCartItemRequest>>(b =>
            {
                // For the sake of efficiency -update orderbook and then persist all events
                var confirmation = new Confirmation(b.ConfirmationId, PersistenceId);
                var bid = b.Message;
                //ProcessBid(bid, confirmation);
            });

            Command<CreateCartItemRequest>(b =>
            {
                //ProcessBid(b, null);
            });
        }

        private void Recovers()
        {
            Recover<SnapshotOffer>(offer =>
            {
                if (offer.Snapshot is List<CartJournal> carts)
                {
                    
                }
            });

            Recover<CreateCartRequest>(b => {
            });

            Recover<CreateCartItemRequest>(a => { 
            });
        }

    }
}
