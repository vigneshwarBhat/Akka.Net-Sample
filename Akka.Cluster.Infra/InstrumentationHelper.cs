using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using System.Diagnostics;
using System.Text;

namespace Akka.Cluster.Infra
{
    public  class InstrumentationHelper
    {
        private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
        public static void AddActivityToRequest(Activity? activity, BaseMessage props, string sourceAPI, string sourceMethod)
        {
            if (activity is not null)
            {
                Propagator.Inject(new PropagationContext(activity.Context, Baggage.Current), props, InjectContextIntoHeader);
                activity.SetTag("api", sourceAPI);
                activity.SetTag("Method", sourceMethod);
            }
        }

        private static void InjectContextIntoHeader(BaseMessage message, string key, string value)
        {
            try
            {
                if(!message.AdditonalInfo.ContainsKey(key)) 
                {
                    message.AdditonalInfo.Add(key, Encoding.UTF8.GetBytes(value));
                }
                else
                {
                    message.AdditonalInfo[key] = Encoding.UTF8.GetBytes(value);
                }
                

            }
            catch (Exception e)
            {
              
            }
        }

        public static IEnumerable<string> ExtractTraceContextFromBasicProperties(BaseMessage props, string key)
        {
            try
            {
                if (props.AdditonalInfo.TryGetValue(key, out var value))
                {
                    var bytes = value as byte[];
                    return new[] { Encoding.UTF8.GetString(bytes) };
                }
            }
            catch (Exception ex)
            {
                //this.logger.LogError(ex, "Failed to extract trace context.");
            }

            return Enumerable.Empty<string>();
        }
    }
}
