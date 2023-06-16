using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Akka.Cluster.Infra.Events
{
    public class GetCartStatus : IWithCartId
    {
        public string CartId { get; set; }
    }
}
