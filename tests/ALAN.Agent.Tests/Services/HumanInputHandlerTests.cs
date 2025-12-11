using ALAN.Shared.Models;
using ALAN.Shared.Services.Queue;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ALAN.Agent.Tests.Services;

public class HumanInputHandlerTests
{
    [Fact]
    public async Task ReceiveAsync_ProcessesMessagesFromQueue()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ALAN.Agent.Services.HumanInputHandler>>();
        var mockStateManager = new Mock<ALAN.Agent.Services.StateManager>(
            Mock.Of<ALAN.Shared.Services.Memory.IShortTermMemoryService>(),
            Mock.Of<ALAN.Shared.Services.Memory.ILongTermMemoryService>());
        var mockQueue = new Mock<IMessageQueue<HumanInput>>();
        
        var handler = new ALAN.Agent.Services.HumanInputHandler(
            mockLogger.Object,
            mockStateManager.Object,
            mockQueue.Object);

        var testInput = new HumanInput
        {
            Type = HumanInputType.PauseAgent,
            Content = "Pause"
        };

        var queueMessage = new QueueMessage<HumanInput>
        {
            MessageId = "msg-123",
            PopReceipt = "receipt-123",
            Content = testInput,
            DequeueCount = 1,
            InsertedOn = DateTimeOffset.UtcNow
        };

        mockQueue
            .Setup(q => q.ReceiveAsync(It.IsAny<int>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QueueMessage<HumanInput>> { queueMessage });

        mockQueue
            .Setup(q => q.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act - using null agent as we're testing queue interaction only
        var result = await handler.ProcessPendingInputsAsync(null!);

        // Assert
        mockQueue.Verify(
            q => q.ReceiveAsync(10, TimeSpan.FromSeconds(60), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPendingInputsAsync_SkipsChatMessages()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ALAN.Agent.Services.HumanInputHandler>>();
        var mockStateManager = new Mock<ALAN.Agent.Services.StateManager>(
            Mock.Of<ALAN.Shared.Services.Memory.IShortTermMemoryService>(),
            Mock.Of<ALAN.Shared.Services.Memory.ILongTermMemoryService>());
        var mockQueue = new Mock<IMessageQueue<HumanInput>>();
        
        var handler = new ALAN.Agent.Services.HumanInputHandler(
            mockLogger.Object,
            mockStateManager.Object,
            mockQueue.Object);

        var chatInput = new HumanInput
        {
            Type = HumanInputType.ChatWithAgent,
            Content = "Hello"
        };

        var queueMessage = new QueueMessage<HumanInput>
        {
            MessageId = "msg-456",
            PopReceipt = "receipt-456",
            Content = chatInput,
            DequeueCount = 1
        };

        mockQueue
            .Setup(q => q.ReceiveAsync(It.IsAny<int>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QueueMessage<HumanInput>> { queueMessage });

        mockQueue
            .Setup(q => q.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await handler.ProcessPendingInputsAsync(null!);

        // Assert - chat messages should be deleted
        mockQueue.Verify(
            q => q.DeleteAsync("msg-456", "receipt-456", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPendingInputsAsync_HandlesDeadLetterMessages()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ALAN.Agent.Services.HumanInputHandler>>();
        var mockStateManager = new Mock<ALAN.Agent.Services.StateManager>(
            Mock.Of<ALAN.Shared.Services.Memory.IShortTermMemoryService>(),
            Mock.Of<ALAN.Shared.Services.Memory.ILongTermMemoryService>());
        var mockQueue = new Mock<IMessageQueue<HumanInput>>();
        
        var handler = new ALAN.Agent.Services.HumanInputHandler(
            mockLogger.Object,
            mockStateManager.Object,
            mockQueue.Object);

        var testInput = new HumanInput
        {
            Type = HumanInputType.PauseAgent,
            Content = "Pause"
        };

        var queueMessage = new QueueMessage<HumanInput>
        {
            MessageId = "msg-789",
            PopReceipt = "receipt-789",
            Content = testInput,
            DequeueCount = 10 // Exceeds max retry count
        };

        mockQueue
            .Setup(q => q.ReceiveAsync(It.IsAny<int>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QueueMessage<HumanInput>> { queueMessage });

        mockQueue
            .Setup(q => q.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await handler.ProcessPendingInputsAsync(null!);

        // Assert - dead-letter message should be deleted
        mockQueue.Verify(
            q => q.DeleteAsync("msg-789", "receipt-789", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void SubmitInput_AddsToInMemoryQueue()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ALAN.Agent.Services.HumanInputHandler>>();
        var mockStateManager = new Mock<ALAN.Agent.Services.StateManager>(
            Mock.Of<ALAN.Shared.Services.Memory.IShortTermMemoryService>(),
            Mock.Of<ALAN.Shared.Services.Memory.ILongTermMemoryService>());
        var mockQueue = new Mock<IMessageQueue<HumanInput>>();
        
        var handler = new ALAN.Agent.Services.HumanInputHandler(
            mockLogger.Object,
            mockStateManager.Object,
            mockQueue.Object);

        var input = new HumanInput
        {
            Type = HumanInputType.UpdatePrompt,
            Content = "New prompt"
        };

        // Act
        var id = handler.SubmitInput(input);

        // Assert
        Assert.NotNull(id);
        Assert.Equal(input.Id, id);
        Assert.Equal(1, handler.GetPendingCount());
    }

    [Fact]
    public void ClearPending_RemovesAllItems()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ALAN.Agent.Services.HumanInputHandler>>();
        var mockStateManager = new Mock<ALAN.Agent.Services.StateManager>(
            Mock.Of<ALAN.Shared.Services.Memory.IShortTermMemoryService>(),
            Mock.Of<ALAN.Shared.Services.Memory.ILongTermMemoryService>());
        var mockQueue = new Mock<IMessageQueue<HumanInput>>();
        
        var handler = new ALAN.Agent.Services.HumanInputHandler(
            mockLogger.Object,
            mockStateManager.Object,
            mockQueue.Object);

        handler.SubmitInput(new HumanInput { Type = HumanInputType.PauseAgent });
        handler.SubmitInput(new HumanInput { Type = HumanInputType.ResumeAgent });

        // Act
        handler.ClearPending();

        // Assert
        Assert.Equal(0, handler.GetPendingCount());
    }
}
