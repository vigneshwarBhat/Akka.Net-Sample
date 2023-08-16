using Akka.Cluster.Infra;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Remote.Hosting;
using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Persistence.SqlServer.Hosting;
using CartStatusProcessor;

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
                                  Port = 9448
                              },
                              clusterOptions: new ClusterOptions
                              {
                                  SeedNodes = new[] { "akka.tcp://cartservice@localhost:9445" },
                                  Roles = new[] { "cartstatusprocessor" }
                              },
                              config: hostContext.Configuration,
                              readinessPort: 11113,
                              pbmPort: 9214)

                          .WithSqlServerPersistence(localConn) // Standard way to create a default persistence journal and snapshot
                          .WithSqlServerPersistence(shardingJournalOptions, shardingSnapshotOptions)
                          .WithShardRegion<CartStatusPersistenceActor>("cartstatusworker", (id) =>
                          {
                              return Props.Create(() => new CartStatusPersistenceActor(id));
                          },
                            new ShardCartStatusMessage(),
                            new ShardOptions
                            {
                                PassivateIdleEntityAfter = TimeSpan.FromMinutes(2),
                                RememberEntities = true,
                                RememberEntitiesStore = isSqlPersistenceEnabled ? RememberEntitiesStore.Eventsourced : RememberEntitiesStore.DData,
                                JournalOptions = isSqlPersistenceEnabled ? shardingJournalOptions : null,
                                SnapshotOptions = isSqlPersistenceEnabled ? shardingSnapshotOptions : null,
                                StateStoreMode = isSqlPersistenceEnabled ? StateStoreMode.Persistence : StateStoreMode.DData,
                                Role = "cartstatusprocessor"
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