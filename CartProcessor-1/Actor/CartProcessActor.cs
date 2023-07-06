using Akka.Actor;
using Akka.Cluster;
using Akka.Cluster.Infra;
using Akka.Cluster.Infra.Events;
using Akka.Cluster.Sharding;
using Akka.DistributedData;
using Akka.Event;
using Akka.Hosting;
using System.Text.Json;

namespace CartWorker.Actor
{
    internal class CartProcessActor : ReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();
        private IActorRef _shardCartItemActor;
        public CartProcessActor(IActorRegistry _actorRegistry)
        {
            _shardCartItemActor = _actorRegistry.Get<IShardProxyActor>();
            var _cluster = Cluster.Get(Context.System);
            var _replicator = DistributedData.Get(Context.System).Replicator;
            var writeConsistency =  WriteLocal.Instance;
            var readConsistency = ReadLocal.Instance;

            Receive<CreateCartRequest>(async req =>
            {
                var str = JsonSerializer.Serialize(new List<CartItemJournal>());
                var response =await _replicator.Ask<IUpdateResponse>(Dsl.Update(new ORSetKey<string>(req.CartId), new ORSet<string>().Add(_cluster, string.Empty), writeConsistency));
                _log.Info($"[{nameof(CartProcessActor)}] Received Create cart request for cart Id {req.CartId}");
            });

            Receive<GetCartStatus>(async req =>
            {
                _log.Info($"Received GetCartStatus for cart Id: {req.CartId}");
                var key = new ORSetKey<string>(req.CartId);
                var sender = Context.Sender;
                var response = await _replicator.Ask<IGetResponse>(Dsl.Get(key, readConsistency));
                var cartItems = new List<CartItemJournal>();
                var cart = new CartJournal
                {
                    CartId = req.CartId,
                    CartStatus = "Completed",
                    CartItemSnapshots = cartItems
                };
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
                    if (cartItems != null && cartItems.Any() && cartItems.All(x => x.Status == "Completed"))
                    {
                        cart.CartStatus = "Completed";
                    }
                }
                sender.Tell(cart);
            });

            Receive<CreateCartItemRequest>(req =>
            {
                _log.Info($"Received CreateCartItemRequest for cart Id: {req.CartId} with item : {req.CartItemId}");
                _shardCartItemActor.Tell(req);
            });
        }

    }
}
