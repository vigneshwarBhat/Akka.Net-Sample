# Akka.Net-Sample implementing akk.cluster sharding.


## CartAPI 
This is a starting point of the application which exposes few api's like POST /Cart, POST /{id}/item and GET cart/status. And this dot.net core service internally uses different actor systems to do its job.

## CartProcessor Actor
 This actor is responsible for processing any request pertaining to cart. Every request with unique cart id will end up creating new actor and processing will be done by that new actor. Any request to with the same cart id, will always be sent to the same actor. It basically uses consistent hashing to find the right actor to make call and process the request.

 ## CartItemProcessor Actor
This actor is responsible for processing any request pertaining to cart item. Every request with unique cart item id will end up creating new actor and processing will be done by that new actor. Any request to with the same cart id, will always be sent to the same actor. It basically uses consistent hashing to find the right actor to make call and process the request.

 ## State management
 The application uses sharding and uses consistent hashing for routing the request. When new pod gets added to cluster, shard rebalancing will happen and some of the shards present in existing pods gets moved to newly added pod along with actors on those shards. The state across various actor instance is mainatined using akk.net in memory distributed data. We can even persist the data to data store which is not implemented yet.

 ## Deployment
 This has been tested to work with k8s with minikube cluster and as well as with docker compose. So you can deploy it to k8s cluster and make sure you create all the resources in a namespace not on default namespace as a best practice. And you can locally run in visual studio, rider or VS code.
