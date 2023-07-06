using Akka.Actor;
using Akka.Cluster;
using Akka.Cluster.Infra;
using Akka.Cluster.Infra.Events;
using Akka.Cluster.Sharding;
using Akka.DistributedData;
using Akka.Event;
using System.Text.Json;

namespace CartWorker.Actor
{
    internal class CartItemProcessActor : ReceiveActor
    {

        private readonly ILoggingAdapter _log = Context.GetLogger();
        private Cluster _cluster;
        private IActorRef _replicator;
        public CartItemProcessActor()
        {
            _cluster = Cluster.Get(Context.System);
            _replicator = DistributedData.Get(Context.System).Replicator;
            var writeConsistency = WriteLocal.Instance;
            var readConsistency = ReadLocal.Instance;


            Receive<CreateCartItemRequest>(async req =>
            {
                _log.Info($"[{nameof(CartItemProcessActor)}] Received Create cart item request for cart Id {req.CartId} and cart item id {req.CartItemId}");
                var key = new ORSetKey<string>(req.CartId);
                var response = await _replicator.Ask<IGetResponse>(Dsl.Get(key, readConsistency));
                var cartItems = new List<CartItemJournal>();
                if (response.IsSuccessful)
                {
                    var data = response.Get(key);
                    foreach (var item in data.Elements)
                    {
                        if (!string.IsNullOrEmpty(item))
                        {
                            cartItems.AddRange(JsonSerializer.Deserialize<List<CartItemJournal>>(item));
                        }
                    }
                }
              
                var cartItem = new CartItemJournal
                {
                    CartItemId = req.CartItemId,
                    Name = req.Name,
                    Status = "InProgress"
                };
                cartItems.Add(cartItem);
                var newData = new ORSet<string>().Add(_cluster, JsonSerializer.Serialize(cartItems));
                await _replicator.Ask<IUpdateResponse>(Dsl.Update(key, newData, writeConsistency));

                //do some processing
                await Task.Delay(1000);
                cartItems.ForEach(item =>
                {
                    if (item != null && item.CartItemId == cartItem.CartItemId)
                    {
                        item.Status = "Completed";
                    }
                });
                var cartString = JsonSerializer.Serialize(cartItems);
                newData = new ORSet<string>().Add(_cluster, cartString);
                var res = await _replicator.Ask<IUpdateResponse>(Dsl.Update(key, newData, writeConsistency));
                _log.Info($"[{nameof(CartItemProcessActor)}] Received Add item request for item Id {req.CartItemId} and for cart Id: {req.CartId}");
            });
        }
    }
}
