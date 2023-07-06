using Akka.Cluster.Infra;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Remote.Hosting;
using Akka.Cluster.Infra.Events;
using Akka.Actor;
using CartWorker.Actor;
using Akka.Cluster.Sharding;

class Program
{
    static async Task Main(string[] args)
    {
            var host = new HostBuilder()
                  .ConfigureHostConfiguration(builder =>
                  {
                      builder.AddEnvironmentVariables();
                  })
                  .ConfigureServices((hostContext, services) =>
                  {
                      services.AddLogging();
                      services.AddAkka("cartservice", (builder, provider) =>
                      {
                          builder
                             .AddHocon(hocon: "akka.remote.dot-netty.tcp.maximum-frame-size = 256000b", addMode: HoconAddMode.Prepend)

                             // Add common DevOps settings
                             .WithOps(
                                  remoteOptions: new RemoteOptions
                                  {
                                      HostName = "0.0.0.0",
                                      Port = 9447
                                  },
                                  clusterOptions: new ClusterOptions
                                  {
                                      SeedNodes = new[] { "akka.tcp://cartservice@localhost:9445" },
                                      Roles = new[] { "cartprocessor" }
                                  },
                                  config: hostContext.Configuration,
                                  readinessPort: 11112,
                                  pbmPort: 9213)

                              .WithShardRegionProxy<IShardProxyActor>("cartitemworker", "cartitemprocessor", new ShardCartItemMessage())
                              .WithShardRegion<IShardActor>("cartworker", (id) =>
                              {
                                  var registry = provider.GetRequiredService<IActorRegistry>();
                                  return Props.Create(() => new CartProcessActor(registry));
                              },
                              new ShardCartMessageRouter(),
                              new ShardOptions
                              {
                                  PassivateIdleEntityAfter = TimeSpan.FromMinutes(2),
                                  RememberEntities = true,
                                  RememberEntitiesStore = RememberEntitiesStore.DData,
                                  StateStoreMode = StateStoreMode.DData,
                                  Role = "cartprocessor"
                              });

                      });
                  })
                  .ConfigureLogging((hostContext, configLogging) =>
                  {
                      configLogging.AddConsole();

                  })
                  .UseConsoleLifetime()
                  .Build();

            await host.RunAsync();

    }
}