namespace SmsMicroservice.Queue;
// Represents an interface for a service that facilitates asynchronous communication 
// between different parts of a microservices architecture via events.
// It allows a service to publish an event to be consumed by other services 
// without knowing their internal details. This helps in keeping the services loosely coupled.
public interface IEventBus<T>
{
    Task PublishAsync(T eventToPublish);
}