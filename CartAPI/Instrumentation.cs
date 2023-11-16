using System.Diagnostics.Metrics;
using System.Diagnostics;

namespace CartAPI
{
    public class Instrumentation : IDisposable
    {
        internal const string ActivitySourceName = "Cart-API";

        public Instrumentation()
        {
            string? version = typeof(Instrumentation).Assembly.GetName().Version?.ToString();
            this.ActivitySource = new ActivitySource(ActivitySourceName, version);
           
        }

        public ActivitySource ActivitySource { get; }


        public void Dispose()
        {
            this.ActivitySource.Dispose();
        }
    }
}
