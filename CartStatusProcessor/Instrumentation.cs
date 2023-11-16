using System.Diagnostics;

namespace CartStatusProcessor
{
    public class Instrumentation : IDisposable
    {
        internal const string ActivitySourceName = "CartStatusProcessor";

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
