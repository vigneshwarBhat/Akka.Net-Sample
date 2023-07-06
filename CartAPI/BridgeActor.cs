using Akka.Actor;
using Akka.Cluster.Infra;
using Akka.Cluster.Infra.Events;
using Akka.Event;
using Akka.Hosting;

namespace TradePlacementAPI
{
    public class BridgeActor:ReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();
        private IActorRef _shardCartActor;

        public BridgeActor(IActorRegistry _actorRegistry)
        {
            _shardCartActor = _actorRegistry.Get<IShardProxyActor>();
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

            Receive<GetCartStatus>(async GetCartStatus =>
            {
                var sender = Context.Sender;
                var result = await _shardCartActor.Ask<CartJournal>(GetCartStatus);               
                _log.Info($"Received Get cart status response for cart Id: {result.CartId} with status : {result.CartStatus}");
                sender.Tell(result);
                
            });
        }


    }
}
