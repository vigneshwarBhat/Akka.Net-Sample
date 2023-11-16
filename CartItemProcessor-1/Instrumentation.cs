using System.Diagnostics;

namespace CartItemProcessor_1
{
    public class Instrumentation : IDisposable
    {
        internal const string ActivitySourceName = "CartItemProcessor";

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
