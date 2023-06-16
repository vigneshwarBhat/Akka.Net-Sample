using Akka.Actor;
using Akka.Cluster;
using Akka.Cluster.Infra;
using Akka.Cluster.Infra.Events;
using Akka.Cluster.Routing;
using Akka.Cluster.Sharding;
using Akka.Event;
using Akka.Routing;

namespace TradePlacementAPI
{
    public class BridgeActor:ReceiveActor
    {
        private IActorRef _cartRouter;
        private readonly ILoggingAdapter _log = Context.GetLogger();
        private IActorRef _shardCartActor;

        public BridgeActor()
        {
            Receive<CreateCartRequest>(cartReq =>
            {
                _log.Info($"Received create cart request for cart Id {cartReq.CartId} for the user : {cartReq.UserId}");
                _shardCartActor.Tell(cartReq);
                Sender.Tell(new CreateCartResponse { CartId = cartReq.CartId, Status = "InProgress" });
            });

            Receive<CreateCartItemRequest>(cartItemReq =>
            {
                _log.Info($"Received add cart item request for item Id {cartItemReq.CartItemId} for the cart : {cartItemReq.CartId}");
                _shardCartActor.Tell(cartItemReq);
                Sender.Tell(new CreateCartItemResponse { CartItemId = cartItemReq.CartItemId, Status="InProgress" });
            });

            Receive<CreateCartResponse>(cartRes =>
            {
                _log.Info($"Received create cart response for cart Id {cartRes.CartId} with status : {cartRes.Status}");
                
            });

            Receive<CreateCartItemResponse>(cartItemRes =>
            {
                _log.Info($"Received create cart item response for item Id: {cartItemRes.CartItemId} with status : {cartItemRes.Status}");
            });

            Receive<GetCartStatus>(async GetCartStatus =>
            {
                var sender = Context.Sender;
                var result = await _shardCartActor.Ask<CartJournal>(GetCartStatus);
                _log.Info($"Received Get cart status response for cart Id: {result.CartId} with status : {result.CartStatus}");
                sender.Tell(result);
                
            });
        }

        protected override void PreStart()
        {
            _cartRouter = Context.System.ActorOf(Props.Empty.WithRouter(new ClusterRouterPool(new ConsistentHashingPool(10,CartEventConsistentHashMapping.consistentHashMappingKey), new ClusterRouterPoolSettings(10,10,true, "cart-processor"))), "cartPoolRouter");

            Cluster.Get(Context.System).RegisterOnMemberUp(() =>
            {
                var sharding = ClusterSharding.Get(Context.System);
                _shardCartActor = sharding.StartProxy("cartWorker", "cart-processor", new ShardCartMessageRouter());
            });
           
            base.PreStart();
        }

        protected override void PostStop()
        {
            base.PostStop();
        }

    }
}
