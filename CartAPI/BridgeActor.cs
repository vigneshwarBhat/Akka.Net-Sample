using Akka.Actor;
using Akka.Cluster.Infra;
using Akka.Cluster.Infra.Events;
using Akka.Cluster.Infra.Events.Persistence;
using Akka.Event;
using Akka.Hosting;
using CartAPI;
using Conga.Platform.DocumentManagement.Client;
using Conga.Platform.DocumentManagement.Client.Models.Request;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using System.Diagnostics;

namespace TradePlacementAPI
{
    public class BridgeActor:ReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();
        private readonly IServiceProvider _serviceProvider;
        private IActorRef _shardCartActor;
        private IActorRef _shardCartStatusActor;
        private readonly ActivitySource _activitySource=new(Instrumentation.ActivitySourceName);
        private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

        public BridgeActor(IActorRegistry _actorRegistry, IServiceProvider serviceProvider)
        {
            _shardCartActor = _actorRegistry.Get<ShardCartMessageRouter>();
            _shardCartStatusActor= _actorRegistry.Get<ShardCartStatusMessage>();
            _serviceProvider = serviceProvider;
            Receive<CreateCartRequest>(async cartReq =>
            {
               // using var scope= _serviceProvider.CreateScope();
                //var _documentManagementClient = scope.ServiceProvider.GetRequiredService<IDocumentManagementClient>();
                //await _documentManagementClient.GetManyAsync(new DocumentGetManyRequest {DocumentIds=new List<string>() });
                var parentContext = Propagator.Extract(default, cartReq, InstrumentationHelper.ExtractTraceContextFromBasicProperties);
                Baggage.Current = parentContext.Baggage;
                using var activity=_activitySource.StartActivity(nameof(BridgeActor), ActivityKind.Internal, parentContext.ActivityContext);
                InstrumentationHelper.AddActivityToRequest(activity, cartReq, "POST api/cart", nameof(BridgeActor));
                _log.Info($"Received create cart request for cart Id {cartReq.CartId} for the user : {cartReq.UserId}");
                _shardCartActor.Tell(cartReq);
                Sender.Tell(new CreateCartResponse { CartId = cartReq.CartId, Status = "InProgress" });
            });

            Receive<CreateCartItemRequest>(cartItemReq =>
            {
                var parentContext = Propagator.Extract(default, cartItemReq, InstrumentationHelper.ExtractTraceContextFromBasicProperties);
                Baggage.Current = parentContext.Baggage;
                using var activity = _activitySource.StartActivity(nameof(BridgeActor), ActivityKind.Internal, parentContext.ActivityContext);
                InstrumentationHelper.AddActivityToRequest(activity, cartItemReq, "POST api/cartid/item", nameof(BridgeActor));
                _log.Info($"Received add cart item request for item Id {cartItemReq.CartItemId} for the cart : {cartItemReq.CartId}");
                _shardCartActor.Tell(cartItemReq);
                Sender.Tell(new CreateCartItemResponse { CartItemId = cartItemReq.CartItemId, Status="InProgress" });
            });

            Receive<GetCartStatus>(async GetCartStatus =>
            {
                var parentContext = Propagator.Extract(default, GetCartStatus, InstrumentationHelper.ExtractTraceContextFromBasicProperties);
                Baggage.Current = parentContext.Baggage;
                using var activity = _activitySource.StartActivity(nameof(BridgeActor), ActivityKind.Internal, parentContext.ActivityContext);
                InstrumentationHelper.AddActivityToRequest(activity, GetCartStatus, "POST api/cartid/status", nameof(BridgeActor));
                var sender = Context.Sender;
                var result = await _shardCartStatusActor.Ask<CartJournal>(GetCartStatus);               
                _log.Info($"Received Get cart status response for cart Id: {result.CartId} with status : {result.CartStatus}");
                sender.Tell(result);
                
            });
        }


    }
}
