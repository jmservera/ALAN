using ALAN.Shared.Models;
using ALAN.Shared.Services.Queue;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ALAN.Shared.Tests.Services.Queue;

public class AzureStorageQueueServiceTests
{
    private readonly Mock<ILogger<AzureStorageQueueService<TestMessage>>> _mockLogger;
    private const string TestConnectionString = "UseDevelopmentStorage=true";
    private const string TestQueueName = "test-queue";

    public AzureStorageQueueServiceTests()
    {
        _mockLogger = new Mock<ILogger<AzureStorageQueueService<TestMessage>>>();
    }

    [Fact(Skip = "Requires Azurite running - integration test")]
    public async Task SendAsync_SerializesAndSendsMessage()
    {
        // Arrange
        var service = new AzureStorageQueueService<TestMessage>(
            TestConnectionString,
            TestQueueName,
            _mockLogger.Object);
        
        await service.InitializeAsync();

        var message = new TestMessage { Id = "test-1", Content = "Test content" };

        // Act
        await service.SendAsync(message);

        // Assert - message should be in queue
        var messages = await service.ReceiveAsync(1);
        Assert.Single(messages);
        Assert.Equal("test-1", messages[0].Content.Id);
        Assert.Equal("Test content", messages[0].Content.Content);

        // Cleanup
        await service.DeleteAsync(messages[0].MessageId, messages[0].PopReceipt);
    }

    [Fact(Skip = "Requires Azurite running - integration test")]
    public async Task ReceiveAsync_DeserializesMessages()
    {
        // Arrange
        var service = new AzureStorageQueueService<TestMessage>(
            TestConnectionString,
            TestQueueName,
            _mockLogger.Object);
        
        await service.InitializeAsync();

        var message1 = new TestMessage { Id = "test-1", Content = "Content 1" };
        var message2 = new TestMessage { Id = "test-2", Content = "Content 2" };
        
        await service.SendAsync(message1);
        await service.SendAsync(message2);

        // Act
        var messages = await service.ReceiveAsync(10);

        // Assert
        Assert.True(messages.Count >= 2);
        
        // Cleanup
        foreach (var msg in messages)
        {
            await service.DeleteAsync(msg.MessageId, msg.PopReceipt);
        }
    }

    [Fact(Skip = "Requires Azurite running - integration test")]
    public async Task DeleteAsync_RemovesMessage()
    {
        // Arrange
        var service = new AzureStorageQueueService<TestMessage>(
            TestConnectionString,
            TestQueueName,
            _mockLogger.Object);
        
        await service.InitializeAsync();

        var message = new TestMessage { Id = "test-delete", Content = "To be deleted" };
        await service.SendAsync(message);

        var messages = await service.ReceiveAsync(1);
        Assert.Single(messages);

        // Act
        await service.DeleteAsync(messages[0].MessageId, messages[0].PopReceipt);

        // Assert - message should be gone
        var remainingMessages = await service.ReceiveAsync(1, TimeSpan.FromSeconds(1));
        Assert.DoesNotContain(remainingMessages, m => m.Content.Id == "test-delete");
    }

    [Fact(Skip = "Requires Azurite running - integration test")]
    public async Task GetApproximateCountAsync_ReturnsCount()
    {
        // Arrange
        var service = new AzureStorageQueueService<TestMessage>(
            TestConnectionString,
            TestQueueName,
            _mockLogger.Object);
        
        await service.InitializeAsync();
        await service.ClearAsync();

        var message1 = new TestMessage { Id = "count-1", Content = "Message 1" };
        var message2 = new TestMessage { Id = "count-2", Content = "Message 2" };
        
        await service.SendAsync(message1);
        await service.SendAsync(message2);

        // Wait a bit for queue statistics to update
        await Task.Delay(1000);

        // Act
        var count = await service.GetApproximateCountAsync();

        // Assert
        Assert.True(count >= 2);

        // Cleanup
        await service.ClearAsync();
    }

    [Fact]
    public void Constructor_CreatesQueueClient()
    {
        // Act
        var service = new AzureStorageQueueService<TestMessage>(
            TestConnectionString,
            TestQueueName,
            _mockLogger.Object);

        // Assert - should not throw
        Assert.NotNull(service);
    }

    [Fact]
    public async Task SendAsync_CreatesQueueIfMissing()
    {
        // Arrange: use a unique queue name to avoid collisions
        var queueName = $"test-queue-{Guid.NewGuid():N}";
        var service = new AzureStorageQueueService<TestMessage>(
            TestConnectionString,
            queueName,
            _mockLogger.Object);

        var message = new TestMessage { Id = "test-1", Content = "Hello" };

        // Act: send without explicit InitializeAsync to verify self-initialization
        await service.SendAsync(message);

        // Assert: message should be retrievable
        var messages = await service.ReceiveAsync(1, TimeSpan.FromSeconds(5));
        Assert.NotEmpty(messages);
        Assert.Equal("test-1", messages[0].Content.Id);

        // Cleanup
        await service.ClearAsync();
    }
}

public class TestMessage
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
