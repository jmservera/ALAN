using ALAN.Shared.Services.Resilience;
using ALAN.Shared.Utilities;
using Azure.Identity;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Logging;
using Polly;
using System.Text.Json;

namespace ALAN.Shared.Services.Queue;

/// <summary>
/// Azure Storage Queue implementation of IMessageQueue.
/// Uses Azure Queue Storage for reliable message queuing.
/// </summary>
public class AzureStorageQueueService<T> : IMessageQueue<T>, IDisposable where T : class
{
    private readonly QueueClient _queueClient;
    private readonly ILogger<AzureStorageQueueService<T>> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;
    private bool _disposed;

    public AzureStorageQueueService(
        string connectionString,
        string queueName,
        ILogger<AzureStorageQueueService<T>> logger)
    {
        _logger = logger;
        
        // Check authentication method: AccountKey, SharedAccessSignature, UseDevelopmentStorage, or managed identity
        if (connectionString.Contains("AccountKey=", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("SharedAccessSignature=", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("UseDevelopmentStorage=", StringComparison.OrdinalIgnoreCase))
        {
            // Traditional connection string (account key, SAS token, or Azurite)
            _queueClient = new QueueClient(connectionString, queueName);
            _logger.LogInformation("Using connection string authentication for Azure Storage Queue: {QueueName}", queueName);
        }
        else
        {
            // Extract account name and use managed identity
            var accountName = AzureStorageConnectionStringHelper.ExtractAccountName(connectionString);
            var queueEndpoint = new Uri($"https://{accountName}.queue.core.windows.net/{queueName}");
            _queueClient = new QueueClient(queueEndpoint, new DefaultAzureCredential());
            _logger.LogInformation("Using managed identity authentication for Azure Storage Queue: {QueueName} on {AccountName}", queueName, accountName);
        }
        
        _resiliencePipeline = ResiliencePolicy.CreateStorageRetryPipeline(logger);
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        _logger.LogInformation("Azure Storage Queue Service created for queue: {QueueName}", queueName);
    }

    /// <summary>
    /// Ensures the queue exists. Should be called during service initialization.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await _resiliencePipeline.ExecuteAsync(async ct =>
                await _queueClient.CreateIfNotExistsAsync(cancellationToken: ct),
                cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task SendAsync(T message, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var json = JsonSerializer.Serialize(message, _jsonOptions);
        var response = await _resiliencePipeline.ExecuteAsync(async ct =>
            await _queueClient.SendMessageAsync(json, ct),
            cancellationToken);
        _logger.LogDebug("Message sent to queue: {MessageId}", response.Value.MessageId);
    }

    public async Task SendAsync(T message, TimeSpan visibilityTimeout, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var json = JsonSerializer.Serialize(message, _jsonOptions);
        var response = await _resiliencePipeline.ExecuteAsync(async ct =>
            await _queueClient.SendMessageAsync(
                json,
                visibilityTimeout: visibilityTimeout,
                cancellationToken: ct),
            cancellationToken);
        _logger.LogDebug("Message sent to queue with {Timeout}s delay: {MessageId}",
            visibilityTimeout.TotalSeconds, response.Value.MessageId);
    }

    public async Task<IReadOnlyList<QueueMessage<T>>> ReceiveAsync(
        int maxMessages = 10,
        TimeSpan? visibilityTimeout = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var timeout = visibilityTimeout ?? TimeSpan.FromSeconds(30);
        var response = await _resiliencePipeline.ExecuteAsync(async ct =>
            await _queueClient.ReceiveMessagesAsync(
                maxMessages,
                timeout,
                ct),
            cancellationToken);

        var messages = new List<QueueMessage<T>>();
        foreach (var msg in response.Value)
        {
            try
            {
                var content = JsonSerializer.Deserialize<T>(msg.MessageText, _jsonOptions);
                if (content != null)
                {
                    messages.Add(new QueueMessage<T>
                    {
                        MessageId = msg.MessageId,
                        PopReceipt = msg.PopReceipt,
                        Content = content,
                        DequeueCount = (int)msg.DequeueCount,
                        InsertedOn = msg.InsertedOn ?? DateTimeOffset.UtcNow,
                        NextVisibleOn = msg.NextVisibleOn
                    });
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize message {MessageId}", msg.MessageId);
            }
        }

        _logger.LogDebug("Received {Count} messages from queue", messages.Count);
        return messages;
    }

    public async Task DeleteAsync(string messageId, string popReceipt, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _resiliencePipeline.ExecuteAsync(async ct =>
            await _queueClient.DeleteMessageAsync(messageId, popReceipt, ct),
            cancellationToken);
        _logger.LogDebug("Message deleted from queue: {MessageId}", messageId);
    }

    public async Task UpdateAsync(
        string messageId,
        string popReceipt,
        TimeSpan visibilityTimeout,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _resiliencePipeline.ExecuteAsync(async ct =>
            await _queueClient.UpdateMessageAsync(
                messageId,
                popReceipt,
                visibilityTimeout: visibilityTimeout,
                cancellationToken: ct),
            cancellationToken);
        _logger.LogDebug("Message visibility updated: {MessageId}", messageId);
    }

    public async Task<int> GetApproximateCountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var properties = await _resiliencePipeline.ExecuteAsync(async ct =>
            await _queueClient.GetPropertiesAsync(ct),
            cancellationToken);
        return (int)properties.Value.ApproximateMessagesCount;
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _resiliencePipeline.ExecuteAsync(async ct =>
            await _queueClient.ClearMessagesAsync(ct),
            cancellationToken);
        _logger.LogInformation("Queue cleared");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _initLock?.Dispose();
        _disposed = true;
    }

    internal bool IsInitialized => _initialized;
}
