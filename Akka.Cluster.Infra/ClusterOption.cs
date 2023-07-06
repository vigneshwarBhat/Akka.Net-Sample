using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Akka.Cluster.Infra
{
    public class ClusterOption
    {
        public string? Ip { get; set; }
        public int? Port { get; set; }
        public string[]? Seeds { get; set; }
        public StartupMethod? StartupMethod { get; set; }
        public DiscoveryOptions? Discovery { get; set; }
        public int? ReadinessPort { get; set; }
        public int? PbmPort { get; set; }
        public bool IsDocker { get; set; } = false;
    }

    public sealed class DiscoveryOptions
    {
        public string? ServiceName { get; set; }
        public List<string>? ConfigEndpoints { get; set; }
    }

    public enum StartupMethod
    {
        SeedNodes,
        ConfigDiscovery,
        KubernetesDiscovery
    }
}
