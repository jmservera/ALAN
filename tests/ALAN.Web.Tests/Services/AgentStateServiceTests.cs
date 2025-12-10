using ALAN.Shared.Models;
using ALAN.Shared.Services.Memory;
using ALAN.Web.Hubs;
using ALAN.Web.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace ALAN.Web.Tests.Services;

public class AgentStateServiceTests
{
    private readonly Mock<IHubContext<AgentHub>> _mockHubContext;
    private readonly Mock<ILogger<AgentStateService>> _mockLogger;
    private readonly Mock<IShortTermMemoryService> _mockShortTermMemory;
    private readonly Mock<ILongTermMemoryService> _mockLongTermMemory;

    public AgentStateServiceTests()
    {
        _mockHubContext = new Mock<IHubContext<AgentHub>>();
        _mockLogger = new Mock<ILogger<AgentStateService>>();
        _mockShortTermMemory = new Mock<IShortTermMemoryService>();
        _mockLongTermMemory = new Mock<ILongTermMemoryService>();

        // Setup SignalR hub context mocks
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        _mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
    }

    [Fact]
    public void GetCurrentState_ReturnsDefaultState()
    {
        // Arrange
        var service = new AgentStateService(
            _mockHubContext.Object,
            _mockLogger.Object,
            _mockShortTermMemory.Object,
            _mockLongTermMemory.Object);

        // Act
        var state = service.GetCurrentState();

        // Assert
        Assert.NotNull(state);
        Assert.Equal(AgentStatus.Idle, state.Status);
    }

    [Fact]
    public async Task GetCurrentState_AfterServiceStarts_UpdatesFromMemory()
    {
        // Arrange
        var expectedState = new AgentState
        {
            Status = AgentStatus.Thinking,
            CurrentGoal = "Test goal"
        };

        _mockShortTermMemory.Setup(m => m.GetAsync<AgentState>("agent:current-state", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedState);

        _mockShortTermMemory.Setup(m => m.GetKeysAsync("thought:*", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        _mockShortTermMemory.Setup(m => m.GetKeysAsync("action:*", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var service = new AgentStateService(
            _mockHubContext.Object,
            _mockLogger.Object,
            _mockShortTermMemory.Object,
            _mockLongTermMemory.Object);

        // Act - Start and give it time to poll once
        using var cts = new CancellationTokenSource();
        var task = service.StartAsync(cts.Token);
        await Task.Delay(600); // Wait longer than polling interval
        cts.Cancel();

        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        var state = service.GetCurrentState();

        // Assert
        Assert.Equal(AgentStatus.Thinking, state.Status);
        Assert.Equal("Test goal", state.CurrentGoal);
    }

    [Fact]
    public async Task Service_PullsThoughtsFromMemory()
    {
        // Arrange
        var thought = new AgentThought
        {
            Id = "thought-1",
            Content = "Test thought",
            Type = ThoughtType.Planning
        };

        var thoughtKeys = new List<string> { "thought:thought-1" };

        _mockShortTermMemory.Setup(m => m.GetAsync<AgentState>("agent:current-state", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentState?)null);

        _mockShortTermMemory.Setup(m => m.GetKeysAsync("thought:*", It.IsAny<CancellationToken>()))
            .ReturnsAsync(thoughtKeys);

        _mockShortTermMemory.Setup(m => m.GetAsync<AgentThought>("thought:thought-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(thought);

        _mockShortTermMemory.Setup(m => m.GetKeysAsync("action:*", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var service = new AgentStateService(
            _mockHubContext.Object,
            _mockLogger.Object,
            _mockShortTermMemory.Object,
            _mockLongTermMemory.Object);

        // Act
        using var cts = new CancellationTokenSource();
        var task = service.StartAsync(cts.Token);
        await Task.Delay(600);
        cts.Cancel();

        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        var state = service.GetCurrentState();

        // Assert
        Assert.Single(state.RecentThoughts);
        Assert.Equal("Test thought", state.RecentThoughts[0].Content);
    }

    [Fact]
    public async Task Service_PullsActionsFromMemory()
    {
        // Arrange
        var action = new AgentAction
        {
            Id = "action-1",
            Name = "Test Action",
            Status = ActionStatus.Running
        };

        var actionKeys = new List<string> { "action:action-1" };

        _mockShortTermMemory.Setup(m => m.GetAsync<AgentState>("agent:current-state", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentState?)null);

        _mockShortTermMemory.Setup(m => m.GetKeysAsync("thought:*", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        _mockShortTermMemory.Setup(m => m.GetKeysAsync("action:*", It.IsAny<CancellationToken>()))
            .ReturnsAsync(actionKeys);

        _mockShortTermMemory.Setup(m => m.GetAsync<AgentAction>("action:action-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(action);

        var service = new AgentStateService(
            _mockHubContext.Object,
            _mockLogger.Object,
            _mockShortTermMemory.Object,
            _mockLongTermMemory.Object);

        // Act
        using var cts = new CancellationTokenSource();
        var task = service.StartAsync(cts.Token);
        await Task.Delay(600);
        cts.Cancel();

        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        var state = service.GetCurrentState();

        // Assert
        Assert.Single(state.RecentActions);
        Assert.Equal("Test Action", state.RecentActions[0].Name);
    }
}
