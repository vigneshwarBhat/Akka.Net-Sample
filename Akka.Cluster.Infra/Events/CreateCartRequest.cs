namespace Akka.Cluster.Infra.Events
{
    public class CreateCartRequest:IWithCartId
    {
        public string CartId { get; set; }
        public string UserId  { get; set;}
    }
}
