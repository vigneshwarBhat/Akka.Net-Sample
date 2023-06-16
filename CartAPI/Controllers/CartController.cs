using Akka.Actor;
using Akka.Cluster.Infra;
using Akka.Cluster.Infra.Events;
using Akka.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace TradePlacementAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CartController : ControllerBase
    {

        private readonly ILogger<CartController> _logger;
        private readonly IActorRegistry _actorRegistry;
        private readonly IActorRef _bridgeActor;


        public CartController(ILogger<CartController> logger, IActorRegistry actorRegistry)
        {
            _logger = logger;
            _actorRegistry = actorRegistry;
            _bridgeActor = _actorRegistry.Get<BridgeActor>();
        }


        [HttpPost]
        public async Task<IActionResult> CreateCart([FromBody] CreateCartRequest createCartRequest)
        {
            var result = await _bridgeActor.Ask<CreateCartResponse>(createCartRequest);
            return Created(uri: new Uri($"http://cart/{result.CartId}"), result);

        }

        [HttpPost("{cartId}/items")]
        public async Task<IActionResult> CreateCartItem([FromBody] CreateCartItemRequest createCartRequest, [FromRoute] string cartId)
        {
            var result = await _bridgeActor.Ask<CreateCartItemResponse>(createCartRequest);
            return Created(uri: new Uri($"http://cart/{createCartRequest.CartId}/{result.CartItemId}"), result);

        }


        [HttpGet("{cartId}/status")]
        public async Task<IActionResult> GetCartStatus([FromRoute] string cartId)
        {
            var cartStatusRequest = new GetCartStatus { CartId = cartId };
            var result = await _bridgeActor.Ask<CartJournal>(cartStatusRequest);
            return Ok(result);

        }
    }
}