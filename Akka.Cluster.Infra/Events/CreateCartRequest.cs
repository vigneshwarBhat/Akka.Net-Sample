namespace Akka.Cluster.Infra.Events
{
    public class CreateCartRequest: BaseMessage, IWithCartId
    {
        public string CartId { get; set; }
        public string UserId  { get; set;}
    }
}
