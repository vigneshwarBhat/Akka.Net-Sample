using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Akka.Cluster.Infra
{
    public class CartJournal: IWithCartId
    {
        public CartJournal() 
        {
            CartItemSnapshots = new();
        }
        public string CartId { get; set; }
        public string CartStatus { get; set; }
        public List<CartItemJournal> CartItemSnapshots { get; set; }
    }

    public class CartItemJournal: IWithCartItemId
    {
        public CartItemJournal() { }
        public string CartItemId { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
    }

}
