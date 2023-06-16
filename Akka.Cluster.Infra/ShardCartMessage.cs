using Akka.Cluster.Sharding;
using Akka.Persistence.Extras;

namespace Akka.Cluster.Infra
{
    public class ShardCartMessageRouter : HashCodeMessageExtractor
    {


        public ShardCartMessageRouter() : this(DefaultShardCount)
        {
        }

        public ShardCartMessageRouter(int maxNumberOfShards) : base(maxNumberOfShards)
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
            if(message is ShardRegion.StartEntity start)
            {
              return  start.EntityId;
            }
            if (message is IConfirmableMessageEnvelope<IWithCartId> envelope)
            {
                return envelope.Message.CartId;
            }

            return null;
        }
    }

    /// <summary>
    /// Marker interface used for routing messages for specific stock IDs
    /// </summary>
    public interface IWithCartId
    {
        /// <summary>
        /// The ticker symbol for a specific stock.
        /// </summary>
        string CartId { get; }
    }
}