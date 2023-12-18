using Akka.Cluster.Infra;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Remote.Hosting;
using Akka.Actor;
using Akka.Cluster.Infra.Events;
using Akka.Cluster.Sharding;
using CartWorker.Actor;
using CartItemProcessor_1.Actor;
using Akka.Persistence.SqlServer.Hosting;
using Akka.Persistence.Hosting;
using OpenTelemetry.Trace;
using CartItemProcessor_1;
using OpenTelemetry.Resources;
using OpenTelemetry.Exporter;
using OpenTelemetry;
using Conga.Platform.Auth.JWT;
using Conga.Platform.ClientSDK.Framework.Http.Registration;
using Conga.Platform.ClientSDK.Framework.Models;
using Conga.Platform.DocumentManagement.Client.Registration;
using Conga.Platform.DocumentManagement.Client;
using System.Reflection;

class Program
{
    static async Task Main(string[] args)
    {

        var host = new HostBuilder()
              .ConfigureHostConfiguration(builder =>
              {
                  builder.AddJsonFile("appsettings.json");
                  builder.AddUserSecrets(Assembly.GetExecutingAssembly(), true);
                  builder.AddEnvironmentVariables();
              })
              .ConfigureServices((hostContext, services) =>
              {
                  Action<ResourceBuilder> configureResource = r => r.AddService(
                    serviceName: hostContext.Configuration.GetValue("ServiceName", defaultValue: "cartitemprocessor")!,
                    serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
                    serviceInstanceId: Environment.MachineName);
                  services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
                  services.AddAuthorization(options => options.RegisterPolicies());
                  PlatformClientExtensions.AddPlatformClientForWorker(services, hostContext.Configuration);
                  services.AddDocumentManagementClientForWorkerSingleton(hostContext.Configuration,
                      new UserOrganizationDetail
                      {
                          OrganizationFriendlyId = "rls-perf-0c777a65-3b21-43f1-b0d3-bc08e80cd7fc",
                          OrganizationId = "0c777a65-3b21-43f1-b0d3-bc08e80cd7fc",
                          UserId = "0b8f4cee-6087-4e64-baad-8445e6dc4170"
                      });
                  services.AddOpenTelemetry()
                  .WithTracing(builder => builder
                      .ConfigureResource(configureResource)
                      .AddSource(Instrumentation.ActivitySourceName)
                      .AddHttpClientInstrumentation()
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
                      var isSqlPersistenceEnabled = hostContext.Configuration.GetValue<bool>("IsSqlPersistenceEnabled");
                      // Custom journal options with the id "sharding"
                      // The absolute id will be "akka.persistence.journal.sharding"
                      var shardingJournalOptions = new SqlServerJournalOptions(isDefaultPlugin: false)
                      {
                          Identifier = "sharding",
                          ConnectionString = shardingConn,
                          AutoInitialize = true,
                          ConnectionTimeout = TimeSpan.FromSeconds(30)
                        
                      };

                      // Custom snapshots options with the id "sharding"
                      // The absolute id will be "akka.persistence.snapshot-store.sharding"
                      var shardingSnapshotOptions = new SqlServerSnapshotOptions(isDefaultPlugin: false)
                      {
                          Identifier = "sharding",
                          ConnectionString = shardingConn,
                          AutoInitialize = true,
                          ConnectionTimeout = TimeSpan.FromSeconds(30)
                      };
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
                          .WithSqlServerPersistence(localConn)// Standard way to create a default persistence journal and snapshot
                          .WithSqlServerPersistence(shardingJournalOptions, shardingSnapshotOptions)
                          //.WithShardRegion<CartItemProcessActor>("cartitemworker", (id) =>
                          //    {
                          //        return Props.Create(() => new CartItemProcessActor());
                          //    },
                          //    new ShardCartItemMessage(),
                          //    new ShardOptions
                          //    {
                          //        PassivateIdleEntityAfter = TimeSpan.FromMinutes(2),
                          //        RememberEntities = true,
                          //        RememberEntitiesStore = RememberEntitiesStore.DData,
                          //        StateStoreMode = StateStoreMode.DData,
                          //        Role = "cartitemprocessor"
                          //    })
                          .WithShardRegion<CartItemPersistenceActor>("cartitemworker", (id) =>
                           {
                               var docmgmt = provider.GetRequiredService<IDocumentManagementClient>();
                               return Props.Create(() => new CartItemPersistenceActor(id, docmgmt));
                           },
                            new ShardCartItemMessage(),
                            new ShardOptions
                             {
                                PassivateIdleEntityAfter = TimeSpan.FromMinutes(2),
                                RememberEntities = true,
                                RememberEntitiesStore = isSqlPersistenceEnabled ? RememberEntitiesStore.Eventsourced : RememberEntitiesStore.DData,
                                JournalOptions = isSqlPersistenceEnabled ? shardingJournalOptions : null,
                                SnapshotOptions = isSqlPersistenceEnabled ? shardingSnapshotOptions : null,
                                StateStoreMode = isSqlPersistenceEnabled ? StateStoreMode.Persistence : StateStoreMode.DData,
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