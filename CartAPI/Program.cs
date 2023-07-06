using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.Cluster.Infra;
using Akka.Cluster.Infra.Events;
using Akka.Hosting;
using Akka.Remote.Hosting;
using TradePlacementAPI;

var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{environment}.json", optional: true)
    .AddEnvironmentVariables();

builder.Logging.ClearProviders().AddConsole();
builder.WebHost.ConfigureServices((context, services) =>
{
    services.AddControllers();
    services.AddAkka("cartservice", (builder, provider) =>
    {
        builder
            .WithRemoting(hostname: "localhost", port: 9445)
            // Add common DevOps settings
            .WithOps(
                remoteOptions: new RemoteOptions
                {
                    HostName = "0.0.0.0",
                    Port = 9445
                },
                clusterOptions: new ClusterOptions
                {
                    SeedNodes = new[] { "akka.tcp://cartservice@localhost:9445" },
                    Roles = new[] { "cartcreator" },
                },
                config: context.Configuration,
                readinessPort: 11110,
                pbmPort: 9211)
            .WithShardRegionProxy<IShardProxyActor>("cartworker", "cartprocessor", new ShardCartMessageRouter())
            // Instantiate actors
            .WithActors((system, registry) =>
            {
               var bridgeActor = system.ActorOf(Props.Create(() => new BridgeActor(registry)), "bridge");
                registry.Register<BridgeActor>(bridgeActor);
            });
    });
});



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
