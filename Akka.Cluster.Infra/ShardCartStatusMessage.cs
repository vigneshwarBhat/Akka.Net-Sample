using Akka.Cluster.Sharding;
using Akka.Persistence.Extras;

namespace Akka.Cluster.Infra
{
    public class ShardCartStatusMessage: HashCodeMessageExtractor
    {

        public ShardCartStatusMessage() : this(DefaultShardCount)
        {
        }

        public ShardCartStatusMessage(int maxNumberOfShards) : base(maxNumberOfShards)
        {
        }
        /// <summary>
        /// 3 nodes hosting cart processor, 10 shards per node.
        /// </summary>
        public const int DefaultShardCount = 30;
        public override string EntityId(object message)
        {
            if (message is IWithCartId cartMsg)
            {
                return cartMsg.CartId;
            }
            if (message is ShardRegion.StartEntity start)
            {
                return start.EntityId;
            }
            if (message is IConfirmableMessageEnvelope<IWithCartId> envelope)
            {
                return envelope.Message.CartId;
            }

            return null;
        }
    }
}
