using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Cluster;
using Akka.Cluster.Infra;
using Akka.Cluster.Sharding;
using Akka.Cluster.Tools.Client;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Configuration;
using CartWorker.Actor;
using Petabridge.Cmd.Cluster;
using Petabridge.Cmd.Cluster.Sharding;
using Petabridge.Cmd.Host;
using Petabridge.Cmd.Remote;

namespace CartWorker
{
    public class CartItemWorker : IHostedService
    {
        private readonly ILogger<CartItemWorker> _logger;
        private readonly IServiceProvider _provider;
        private readonly IHostApplicationLifetime _lifetime;
        private ActorSystem _actorSystem;

        public CartItemWorker(ILogger<CartItemWorker> logger, IServiceProvider provider, IHostApplicationLifetime lifetime)
        {
            _logger = logger;
            _provider = provider;
            _lifetime = lifetime;
        }

        public  Task StartAsync(CancellationToken cancellationToken)
        {
            var config = File.ReadAllText("app.conf");
            var conf = ConfigurationFactory.ParseString(config);
            
            _actorSystem = ActorSystem.Create("ClusterSys",conf
                .SafeWithFallback(ClusterSharding.DefaultConfig()
                .SafeWithFallback(ClusterClientReceptionist.DefaultConfig())
                .SafeWithFallback(DistributedPubSub.DefaultConfig())));

            Cluster.Get(_actorSystem).RegisterOnMemberUp(() =>
            {
                var sharding = ClusterSharding.Get(_actorSystem);

                var shardRegion = sharding.Start("cartItemWorker",Props.Create(() => new CartItemProcessActor()), ClusterShardingSettings.Create(_actorSystem),
                    new ShardCartItemMessage());
            });

            // need to guarantee that host shuts down if ActorSystem shuts down
            _actorSystem.WhenTerminated.ContinueWith(tr =>
            {
                _lifetime.StopApplication();
            });

            // start Petabridge.Cmd (for external monitoring / supervision)
            var pbm = PetabridgeCmd.Get(_actorSystem);
            pbm.RegisterCommandPalette(ClusterCommands.Instance);
            pbm.RegisterCommandPalette(ClusterShardingCommands.Instance);
            pbm.RegisterCommandPalette(RemoteCommands.Instance());
            pbm.Start();

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _actorSystem.Terminate();
        }     
    }
}