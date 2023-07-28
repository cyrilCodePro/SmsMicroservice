namespace SmsMicroservice.Queue;

// Represents an interface for a service that enables asynchronous, often persistent, message-based communication
// between different parts of a microservices architecture.
// A service can enqueue a message into the queue, and the consumer services can dequeue and process these messages at their own pace.
// This pattern helps in decoupling the services, ensuring high availability and fault tolerance.
public interface IMessageQueue<T>
{
    Task EnqueueAsync(T message);
    Task<T> DequeueAsync();
}