﻿using Akka.Cluster.Infra.Events.Persistence;

namespace Akka.Cluster.Infra.Events
{
    public class CreateCartItemRequest:BaseMessage, IWithCartId, IWithCartItemId
    {
        public string CartId { get; set; }
        public string CartItemId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

    }
}
