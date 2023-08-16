namespace Akka.Cluster.Infra
{
    public class CartJournal
    {
        public CartJournal() 
        {
            CartItemSnapshots = new();
        }

        public string CartId { get; set; }
        public string CartStatus { get; set; }
        public List<CartItemJournal> CartItemSnapshots { get; set; }= new();
    }

    public class CartItemJournal
    {
        public CartItemJournal() { }
        public string CartItemId { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; }
    }

}
