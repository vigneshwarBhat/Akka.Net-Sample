using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Diagnostics;

namespace CartAPI
{
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly CorrelationIdOptions _options;

        public CorrelationIdMiddleware(RequestDelegate next, IOptions<CorrelationIdOptions> options)
        {
            if (options == null)
            {
                _options = new CorrelationIdOptions();
            }

            _next = next ?? throw new ArgumentNullException(nameof(next));

            _options = options.Value;
        }

        public Task Invoke(HttpContext context)
        {
            if (context.Request.Headers.TryGetValue(_options.Header, out StringValues correlationId))
            {
                context.TraceIdentifier = correlationId;
            }
            else
            {
                context.TraceIdentifier = Activity.Current?.Context.TraceId.ToString();
            }

            if (_options.IncludeInResponse)
            {
                // apply the correlation ID to the response header for client side tracking
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers.Add(_options.Header, new[] { Activity.Current?.Context.TraceId.ToString() });
                    return Task.CompletedTask;
                });
            }

            return _next(context);
        }
    }
}
