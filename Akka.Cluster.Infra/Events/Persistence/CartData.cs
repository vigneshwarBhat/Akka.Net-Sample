using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Akka.Cluster.Infra.Events.Persistence
{
    public class CartData
    {
        public List<CartEvent> CartEvents { get; private set; }

        public List<CartItemEvent> CartItemEvents { get; private set; }
        public CartData()
        {
            CartEvents = new List<CartEvent>();
            CartItemEvents = new List<CartItemEvent>();
        }
    }
}
