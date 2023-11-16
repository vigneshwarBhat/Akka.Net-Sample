using Akka.Actor;
using Akka.Cluster.Infra;
using Akka.Cluster.Infra.Events;
using Akka.Hosting;
using CartAPI;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using System.Diagnostics;

namespace TradePlacementAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CartController : ControllerBase
    {

        private readonly ILogger<CartController> _logger;
        private readonly IActorRegistry _actorRegistry;
        private readonly IActorRef _bridgeActor;
        private readonly ActivitySource _activitySource = new(Instrumentation.ActivitySourceName);
       

        public CartController(ILogger<CartController> logger, IActorRegistry actorRegistry)
        {
            _logger = logger;
            _actorRegistry = actorRegistry;
            _bridgeActor = _actorRegistry.Get<BridgeActor>();
        }


        [HttpPost]
        public async Task<IActionResult> CreateCart([FromBody] CreateCartRequest createCartRequest)
        {
            using var activity = _activitySource.StartActivity(nameof(CartController), ActivityKind.Server);
            InstrumentationHelper.AddActivityToRequest(activity, createCartRequest, "POST api/cart", nameof(CreateCart));
            var result = await _bridgeActor.Ask<CreateCartResponse>(createCartRequest);
            return Created(uri: new Uri($"http://cart/{result.CartId}"), result);

        }

        [HttpPost("{cartId}/items")]
        public async Task<IActionResult> CreateCartItem([FromBody] CreateCartItemRequest createCartRequest, [FromRoute] string cartId)
        {
            using var activity = _activitySource.StartActivity(nameof(CartController), ActivityKind.Server);
            InstrumentationHelper.AddActivityToRequest(activity, createCartRequest, "POST api/{cartid}/items", nameof(CreateCartItem));
            var result = await _bridgeActor.Ask<CreateCartItemResponse>(createCartRequest);
            return Created(uri: new Uri($"http://cart/{createCartRequest.CartId}/{result.CartItemId}"), result);

        }


        [HttpGet("{cartId}/status")]
        public async Task<IActionResult> GetCartStatus([FromRoute] string cartId)
        {
            using var activity = _activitySource.StartActivity(nameof(CartController), ActivityKind.Server);
           
            var cartStatusRequest = new GetCartStatus { CartId = cartId };
            InstrumentationHelper.AddActivityToRequest(activity, cartStatusRequest, "POST api/{cartid}/status", nameof(GetCartStatus));
            var result = await _bridgeActor.Ask<CartJournal>(cartStatusRequest);
            return Ok(result);

        }

    }
}