using CartWorker;
using Microsoft.AspNetCore;

namespace Akka.CQRS.TradeProcessor.Service
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = WebHost.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddLogging();
                    services.AddHostedService<AkkaService>();
                })
                .ConfigureLogging((hostContext, configLogging) =>
                {
                    configLogging.AddConsole();
                })
                .Configure(app =>
                {
                    app.UseRouting();
                })
                .Build();

            await host.RunAsync();
        }
    }
}
