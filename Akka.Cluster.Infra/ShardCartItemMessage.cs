using Akka.Cluster.Sharding;
using Akka.Persistence.Extras;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Akka.Cluster.Infra
{
    public class ShardCartItemMessage : HashCodeMessageExtractor
    {

        public ShardCartItemMessage() : this(DefaultShardCount)
        {
        }

        public ShardCartItemMessage(int maxNumberOfShards) : base(maxNumberOfShards)
        {
        }
        /// <summary>
        /// 3 nodes hosting cart processor, 10 shards per node.
        /// </summary>
        public const int DefaultShardCount = 30;
        public override string EntityId(object message)
        {
            if (message is IWithCartItemId cartMsg)
            {
                return cartMsg.CartItemId;
            }
            if (message is ShardRegion.StartEntity start)
            {
                return start.EntityId;
            }
            if (message is IConfirmableMessageEnvelope<IWithCartItemId> envelope)
            {
                return envelope.Message.CartItemId;
            }

            return null;
        }
    }

    /// <summary>
    /// Marker interface used for routing messages for specific stock IDs
    /// </summary>
    public interface IWithCartItemId
    {
        /// <summary>
        /// The ticker symbol for a specific stock.
        /// </summary>
        string CartItemId { get; }
    }
}
