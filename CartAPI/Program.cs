using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.Cluster.Routing;
using Akka.Hosting;
using Akka.Remote.Hosting;
using Akka.Routing;
using Petabridge.Cmd.Cluster;
using Petabridge.Cmd.Cluster.Sharding;
using Petabridge.Cmd.Host;
using Petabridge.Cmd.Remote;
using System.Diagnostics;
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

var akkaConfig = builder.Configuration.GetRequiredSection(nameof(AkkaClusterConfig))
    .Get<AkkaClusterConfig>();

builder.Services.AddControllers();
builder.Services.AddAkka(akkaConfig.ActorSystemName, (builder, provider) =>
{
    Debug.Assert(akkaConfig.Port != null, "akkaConfig.Port != null");
    builder.AddHoconFile("app.conf", HoconAddMode.Append)
        .WithRemoting(akkaConfig.Hostname, akkaConfig.Port.Value)
        .WithClustering(new ClusterOptions()
        {
            Roles = akkaConfig.Roles,
            SeedNodes = akkaConfig.SeedNodes
        })
        .AddPetabridgeCmd(cmd =>
        {
            cmd.RegisterCommandPalette(new RemoteCommands());
            cmd.RegisterCommandPalette(ClusterCommands.Instance);

            // sharding commands, although the app isn't configured to host any by default
            cmd.RegisterCommandPalette(ClusterShardingCommands.Instance);
        })
        .WithActors((system, registry) =>
        {
            var consoleActor = system.ActorOf(Props.Create(() => new BridgeActor()), "bridge");
            registry.Register<BridgeActor>(consoleActor);

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
