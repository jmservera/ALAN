namespace ALAN.Shared.Services.Queue;

/// <summary>
/// Abstraction for message queue operations.
/// Supports multiple implementations (Azure Storage Queue, Redis, Service Bus, EventHubs).
/// </summary>
/// <typeparam name="T">The type of message to queue</typeparam>
public interface IMessageQueue<T> where T : class
{
    /// <summary>
    /// Send a message to the queue.
    /// </summary>
    Task SendAsync(T message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a message to the queue with a visibility timeout (delay before processing).
    /// </summary>
    Task SendAsync(T message, TimeSpan visibilityTimeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Receive messages from the queue.
    /// </summary>
    /// <param name="maxMessages">Maximum number of messages to receive</param>
    /// <param name="visibilityTimeout">How long the message should remain invisible to other consumers</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of queue messages</returns>
    Task<IReadOnlyList<QueueMessage<T>>> ReceiveAsync(
        int maxMessages = 10,
        TimeSpan? visibilityTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a message from the queue after processing.
    /// </summary>
    Task DeleteAsync(string messageId, string popReceipt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update a message's visibility timeout (to extend processing time).
    /// </summary>
    Task UpdateAsync(string messageId, string popReceipt, TimeSpan visibilityTimeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get approximate count of messages in the queue.
    /// </summary>
    Task<int> GetApproximateCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear all messages from the queue.
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a message received from the queue.
/// </summary>
public class QueueMessage<T> where T : class
{
    /// <summary>
    /// Unique identifier for the message.
    /// </summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// Receipt handle used for deleting or updating the message.
    /// </summary>
    public string PopReceipt { get; set; } = string.Empty;

    /// <summary>
    /// The actual message content.
    /// </summary>
    public T Content { get; set; } = default!;

    /// <summary>
    /// Number of times this message has been dequeued.
    /// </summary>
    public int DequeueCount { get; set; }

    /// <summary>
    /// When the message was inserted into the queue.
    /// </summary>
    public DateTimeOffset InsertedOn { get; set; }

    /// <summary>
    /// When the message will become visible again (if not deleted).
    /// </summary>
    public DateTimeOffset? NextVisibleOn { get; set; }
}
