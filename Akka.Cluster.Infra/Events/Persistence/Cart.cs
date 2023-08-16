namespace Akka.Cluster.Infra.Events.Persistence
{
    public class CartEvent : ICartEvent
    {
        public string CartId {get;set; }
        public string Status { get; set; }
        public DateTime TimePlaced { get; set; } = DateTime.UtcNow;  
    }

    public class CartItemEvent : ICartEvent
    {
        public string CartId { get; set; }
        public string CartItemId { get; set; }
        public int Quantity { get; set; } = 1;
        public string Status { get; set; } = "InProgress";
        public DateTime TimePlaced { get; set; } = DateTime.UtcNow;
    }

    public interface ICartEvent
    {

    }

}
