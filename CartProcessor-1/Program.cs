using Akka.Cluster.Infra;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Remote.Hosting;
using Akka.Cluster.Infra.Events;
using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Persistence.SqlServer.Hosting;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry;
using CartWorker.Actor;
using System.Diagnostics;

class Program
{
  
    static async Task Main(string[] args)
    {

        var host = new HostBuilder()
              .ConfigureHostConfiguration(builder =>
              {
                  builder.AddJsonFile("appsettings.json");
                  builder.AddEnvironmentVariables();
              })
              .ConfigureServices((hostContext, services) =>
              {
                  Action<ResourceBuilder> configureResource = r => r.AddService(
                    serviceName: hostContext.Configuration.GetValue("ServiceName", defaultValue: "cartprocessor")!,
                    serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
                    serviceInstanceId: Environment.MachineName);

                  services.AddOpenTelemetry()
                   .WithTracing(builder => builder
                       .ConfigureResource(configureResource)
                       .AddSource(Instrumentation.ActivitySourceName)
                       .AddAspNetCoreInstrumentation()
                        //.AddConsoleExporter()
                       .AddOtlpExporter(opts =>
                          {
                              opts.Protocol = OtlpExportProtocol.Grpc;
                              opts.Endpoint = new Uri("http://localhost:4317/api/traces");
                              opts.ExportProcessorType = ExportProcessorType.Batch;
                          }));
                  services.AddLogging();
                  services.AddAkka("cartservice", (builder, provider) =>
                  {
                      // Grab connection strings from appsettings.json
                      var localConn = hostContext.Configuration.GetConnectionString("sqlServerLocal");
                      var shardingConn = hostContext.Configuration.GetConnectionString("sqlServerSharding");

                      // Custom journal options with the id "sharding"
                      // The absolute id will be "akka.persistence.journal.sharding"
                      var shardingJournalOptions = new SqlServerJournalOptions(isDefaultPlugin: false)
                      {
                          Identifier = "sharding",
                          ConnectionString = shardingConn,
                          AutoInitialize = true
                      };

                      // Custom snapshots options with the id "sharding"
                      // The absolute id will be "akka.persistence.snapshot-store.sharding"
                      var shardingSnapshotOptions = new SqlServerSnapshotOptions(isDefaultPlugin: false)
                      {
                          Identifier = "sharding",
                          ConnectionString = shardingConn,
                          AutoInitialize = true
                      };
                      var isSqlPersistenceEnabled = hostContext.Configuration.GetValue<bool>("IsSqlPersistenceEnabled");

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

                          .WithSqlServerPersistence(localConn) // Standard way to create a default persistence journal and snapshot
                          .WithSqlServerPersistence(shardingJournalOptions, shardingSnapshotOptions)
                          .WithShardRegionProxy<IShardProxyActor>("cartitemworker", "cartitemprocessor", new ShardCartItemMessage())
                          //Uncomment if you want to use  akka distributed data for storing state.
                          //.WithShardRegion<CartProcessActor>("cartworker", (id) =>
                          //{
                          //    var registry = provider.GetRequiredService<IActorRegistry>();
                          //    return Props.Create(() => new CartProcessActor(registry));
                          //},
                          //new ShardCartMessageRouter(),
                          //new ShardOptions
                          //{
                          //    PassivateIdleEntityAfter = TimeSpan.FromMinutes(2),
                          //    RememberEntities = true,
                          //    RememberEntitiesStore = RememberEntitiesStore.DData,
                          //    StateStoreMode = StateStoreMode.DData,
                          //    Role = "cartprocessor"
                          //})
                          //Use this, if you want to use  sql as a persistent for storing state.
                          .WithShardRegion<CartProcessorPersistentActor>("cartworker", (id) =>
                           {
                               var registry = provider.GetRequiredService<IActorRegistry>();
                               return Props.Create(() => new CartProcessorPersistentActor(registry, id));
                           },
                            new ShardCartMessageRouter(),
                            new ShardOptions
                            {
                                PassivateIdleEntityAfter = TimeSpan.FromMinutes(2),
                                RememberEntities = true,
                                RememberEntitiesStore = isSqlPersistenceEnabled ? RememberEntitiesStore.Eventsourced : RememberEntitiesStore.DData,
                                JournalOptions = isSqlPersistenceEnabled ? shardingJournalOptions : null,
                                SnapshotOptions = isSqlPersistenceEnabled ? shardingSnapshotOptions : null,
                                StateStoreMode = isSqlPersistenceEnabled ? StateStoreMode.Persistence : StateStoreMode.DData,
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