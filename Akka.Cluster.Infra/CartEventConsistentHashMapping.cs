using Akka.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Akka.Cluster.Infra
{
    public static class CartEventConsistentHashMapping
    {
        public static readonly ConsistentHashMapping consistentHashMappingKey = msg =>
            {
                if (msg is IWithCartId carMsg)
                {
                    return carMsg.CartId;
                }
                return msg.ToString();
            };
    }
}
