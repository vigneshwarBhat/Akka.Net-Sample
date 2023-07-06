using Akka.Cluster.Infra;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Remote.Hosting;
using Akka.Actor;
using Akka.Cluster.Infra.Events;
using Akka.Cluster.Sharding;
using CartWorker.Actor;

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
                                  Port = 9446
                              },
                              clusterOptions: new ClusterOptions
                              {
                                  SeedNodes = new[] { "akka.tcp://cartservice@localhost:9445" },
                                  Roles = new[] { "cartitemprocessor" }
                              },
                              config: hostContext.Configuration,
                              readinessPort: 11111,
                              pbmPort: 9212)
                          .WithShardRegion<IShardActor>("cartitemworker", (id) =>
                              {
                                  return Props.Create(() => new CartItemProcessActor());
                              },
                              new ShardCartItemMessage(),
                              new ShardOptions
                              {
                                  PassivateIdleEntityAfter = TimeSpan.FromMinutes(2),
                                  RememberEntities = true,
                                  RememberEntitiesStore = RememberEntitiesStore.DData,
                                  StateStoreMode = StateStoreMode.DData,
                                  Role = "cartitemprocessor"
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