using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.Cluster.Infra;
using Akka.Hosting;
using Akka.Remote.Hosting;
using CartAPI;
using Conga.Platform.ClientSDK.Framework.Http.Registration;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TradePlacementAPI;
using Conga.Platform.Auth.JWT;
using Conga.Platform.DocumentManagement.Client.Registration;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.OpenApi.Models;
using Conga.Platform.DocumentManagement.Client;
using Conga.Platform.Telemetry;
using Conga.Platform.Telemetry.Contracts.Models;
using System.Reflection;

var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
// Note: Switch between Zipkin/OTLP/Console by setting UseTracingExporter in appsettings.json.
var tracingExporter = builder.Configuration.GetValue("UseTracingExporter", defaultValue: "console")!.ToLowerInvariant();

// Build a resource configuration action to set service information.
Action<ResourceBuilder> configureResource = r => r.AddService(
    serviceName: builder.Configuration.GetValue("ServiceName", defaultValue: "cart-api")!,
    serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
    serviceInstanceId: Environment.MachineName);
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
// Configure OpenTelemetry tracing & metrics with auto-start using the
// AddOpenTelemetry extension from OpenTelemetry.Extensions.Hosting.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(configureResource)
    .WithTracing(appBuilder =>
    {
        // Tracing
        // Ensure the TracerProvider subscribes to any custom ActivitySources.
        appBuilder
            .AddSource(Instrumentation.ActivitySourceName)
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation();

        switch (tracingExporter)
        {
            case "otlp":
                appBuilder.AddOtlpExporter(otlpOptions =>
                {
                    // Use IConfiguration directly for Otlp exporter endpoint option.
                    otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317/api/traces")!);
                    otlpOptions.Protocol = OtlpExportProtocol.Grpc;
                    otlpOptions.ExportProcessorType = ExportProcessorType.Batch;
                });
                break;

            default:
                appBuilder.AddConsoleExporter();
                break;
        }
    });
   
// Clear default logging providers used by WebApplication host.
builder.Logging.ClearProviders();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        In = ParameterLocation.Header,
        Description = "Please insert JWT with Bearer into field",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            },
                            Scheme = "oauth2",
                            Name = "Bearer",
                            In = ParameterLocation.Header
                        },
                        new List<string>()
                    }
                });
});
builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddUserSecrets(Assembly.GetExecutingAssembly(), true)
    .AddEnvironmentVariables();

builder.Logging.ClearProviders().AddConsole();
builder.WebHost.ConfigureServices((context, services) =>
{
    //services.AddControllers();
    services.AddJwtAuthentication(context.Configuration);
    services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
    services.AddAuthorization(options => options.RegisterPolicies());
    PlatformClientExtensions.AddPlatformClientForWeb(services, context.Configuration);
    services.AddDocumentManagementClientForWeb(context.Configuration);
    //services.AddTelemetry(new ServiceInfo
    //{
    //    DisplayName = "Cart",
    //    EnvironmentName = "Test",
    //    Namespace = "Cart",
    //    Version = "1.0.0",
    //    Name="Cart"
    //}, context.Configuration);
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("CorsPolicy",
            policy => policy
                .SetIsOriginAllowed((host) => true)
                .AllowAnyMethod()
            .AllowAnyHeader()
                .AllowCredentials());
    });

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
            .WithShardRegionProxy<ShardCartMessageRouter>("cartworker", "cartprocessor", new ShardCartMessageRouter())
            .WithShardRegionProxy<ShardCartStatusMessage>("cartstatusworker", "cartstatusprocessor", new ShardCartStatusMessage())
            // Instantiate actors
            .WithActors((system, registry) =>
            {
               var bridgeActor = system.ActorOf(Props.Create(() => new BridgeActor(registry, provider)), "bridge");
               registry.Register<BridgeActor>(bridgeActor);               
            });
    });
});
var app = builder.Build();
app.UseCorrelationId();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("CorsPolicy");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireAuthorization();

app.Run();
