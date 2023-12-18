using Akka.Actor;
using Akka.Cluster.Infra;
using Akka.Cluster.Infra.Events;
using Akka.Hosting;
using CartAPI;
using Conga.Platform.DocumentManagement.Client;
using Conga.Platform.DocumentManagement.Client.Models.Request;
using Conga.Platform.DocumentManagement.Client.Models.Response;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using System.Diagnostics;
using System.Reflection.Metadata;
using Document = Akka.Cluster.Infra.Document;

namespace TradePlacementAPI.Controllers
{
    [ApiController]
    [Route("cart")]
    public class CartController : ControllerBase
    {

        private readonly ILogger<CartController> _logger;
        private readonly IDocumentManagementClient _documentManagementClient;
        private readonly IActorRegistry _actorRegistry;
        private readonly IActorRef _bridgeActor;
        private readonly ActivitySource _activitySource = new(Instrumentation.ActivitySourceName);


        public CartController(ILogger<CartController> logger, IActorRegistry actorRegistry, IDocumentManagementClient documentManagementClient)
        {
            _logger = logger;
            _actorRegistry = actorRegistry;
            _bridgeActor = _actorRegistry.Get<BridgeActor>();
            _documentManagementClient = documentManagementClient;
        }


        [HttpPost]
        public async Task<IActionResult> CreateCart([FromBody] CreateCartRequest createCartRequest)
        {
            using var activity = _activitySource.StartActivity(nameof(CartController), ActivityKind.Server);
            InstrumentationHelper.AddActivityToRequest(activity, createCartRequest, "POST api/cart", nameof(CreateCart));
            var result = await _bridgeActor.Ask<CreateCartResponse>(createCartRequest);
            return Created(uri: new Uri($"http://cart/{result.CartId}"), result);

        }

        [HttpPost("{cartid}/items")]
        public async Task<IActionResult> CreateCartItem([FromBody] CreateCartItemRequest createCartRequest, [FromRoute] string cartId)
        {
            using var activity = _activitySource.StartActivity(nameof(CartController), ActivityKind.Server);
            InstrumentationHelper.AddActivityToRequest(activity, createCartRequest, "POST api/{cartid}/items", nameof(CreateCartItem));
            var result = await _bridgeActor.Ask<CreateCartItemResponse>(createCartRequest);
            return Created(uri: new Uri($"http://cart/{createCartRequest.CartId}/{result.CartItemId}"), result);

        }


        [HttpGet("{cartid}/status")]
        public async Task<IActionResult> GetCartStatus([FromRoute] string cartId)
        {
            using var activity = _activitySource.StartActivity(nameof(CartController), ActivityKind.Server);

            var cartStatusRequest = new GetCartStatus { CartId = cartId };
            InstrumentationHelper.AddActivityToRequest(activity, cartStatusRequest, "POST api/{cartid}/status", nameof(GetCartStatus));
            var result = await _bridgeActor.Ask<CartJournal>(cartStatusRequest);
            return Ok(result);

        }

        [HttpGet("docs")]
        public async Task<IActionResult> GetDocsMany()
        {
            try
            {
                using var activity = _activitySource.StartActivity(nameof(CartController), ActivityKind.Server);
                var document = await JsonHelper.ReadAsync<Document>("doc.json");
                var randomDocIds = document.DocumentIds.OrderBy(x => Guid.NewGuid()).ToList().Take(5000);
                var taskList = new List<Task<DocumentGetManyResponse>>();
                activity?.AddEvent(new ActivityEvent($"started sending requests to doc management at {DateTime.Now}."));
                for (var i = 0; i < 50; i++)
                {
                    var docIds = randomDocIds.Skip(i * 100).Take(100).ToList();
                    activity?.SetTag("ids", docIds);
                    activity?.AddEvent(new ActivityEvent($"Document management call number {i} at {DateTime.Now}"));
                    taskList.Add(_documentManagementClient.GetManyAsync(new DocumentGetManyRequest
                    {
                        DocumentIds = docIds
                    }));
                }
                var result = await Task.WhenAll(taskList);
                activity?.AddEvent(new ActivityEvent($"Got a response for all 50 calls for doc management at {DateTime.Now}."));
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return StatusCode(500, ex.Message);
            }
        }

    }
}