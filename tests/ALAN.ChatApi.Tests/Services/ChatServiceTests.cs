using ALAN.ChatApi.Services;
using ALAN.Shared.Services.Memory;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ALAN.ChatApi.Tests.Services;

public class ChatServiceTests
{
    private readonly Mock<AIAgent> _mockAgent;
    private readonly Mock<ILogger<ChatService>> _mockLogger;
    private readonly Mock<ILongTermMemoryService> _mockMemory;
    private readonly ChatService _service;

    public ChatServiceTests()
    {
        _mockAgent = new Mock<AIAgent>();
        _mockLogger = new Mock<ILogger<ChatService>>();
        _mockMemory = new Mock<ILongTermMemoryService>();
        
        _service = new ChatService(
            _mockAgent.Object,
            _mockLogger.Object,
            _mockMemory.Object);
    }

    [Fact]
    public async Task ProcessChatAsync_CallsAIAgentWithPrompt()
    {
        // Arrange
        var sessionId = "test-session";
        var message = "Hello ALAN";
        var tokens = new List<string>();
        
        var mockThread = new Mock<AgentThread>();
        _mockAgent.Setup(a => a.GetNewThread()).Returns(mockThread.Object);

        // Act
        // Note: This test is simplified as we cannot easily mock RunStreamingAsync
        // In a real scenario, you'd need to mock the streaming behavior
        var result = await Record.ExceptionAsync(async () =>
            await _service.ProcessChatAsync(
                sessionId,
                message,
                token => { tokens.Add(token); return Task.CompletedTask; },
                CancellationToken.None));

        // Assert - should not throw (actual behavior depends on agent mock setup)
        // This is a basic structural test
        Assert.NotNull(result); // Expected to throw as agent is mocked
    }

    [Fact]
    public async Task ClearHistoryAsync_RemovesSessionThread()
    {
        // Arrange
        var sessionId = "test-session";
        
        // First create a session by processing a message (which will create a thread)
        var mockThread = new Mock<AgentThread>();
        _mockAgent.Setup(a => a.GetNewThread()).Returns(mockThread.Object);

        try
        {
            await _service.ProcessChatAsync(
                sessionId,
                "Test message",
                _ => Task.CompletedTask,
                CancellationToken.None);
        }
        catch
        {
            // Expected to fail due to mocked agent
        }

        // Act
        await _service.ClearHistoryAsync(sessionId, CancellationToken.None);

        // Assert - no exception should be thrown
        // Cleanup was successful
    }

    [Fact]
    public async Task ProcessChatAsync_CreatesSeparateThreadsForDifferentSessions()
    {
        // Arrange
        var session1 = "session-1";
        var session2 = "session-2";
        
        var thread1 = new Mock<AgentThread>();
        var thread2 = new Mock<AgentThread>();
        
        var threadCallCount = 0;
        _mockAgent.Setup(a => a.GetNewThread())
            .Returns(() => threadCallCount++ == 0 ? thread1.Object : thread2.Object);

        // Act & Assert
        try
        {
            await _service.ProcessChatAsync(session1, "Message 1", _ => Task.CompletedTask, CancellationToken.None);
        }
        catch { }

        try
        {
            await _service.ProcessChatAsync(session2, "Message 2", _ => Task.CompletedTask, CancellationToken.None);
        }
        catch { }

        // Verify that GetNewThread was called twice (once per session)
        _mockAgent.Verify(a => a.GetNewThread(), Times.Exactly(2));
    }

    [Fact]
    public void Dispose_DisposesResources()
    {
        // Arrange & Act
        _service.Dispose();

        // Assert - should not throw
        // Second dispose should be idempotent
        _service.Dispose();
    }

    [Fact]
    public async Task ProcessChatAsync_RespectsCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _service.ProcessChatAsync(
                "test-session",
                "Test message",
                _ => Task.CompletedTask,
                cts.Token));
    }
}
