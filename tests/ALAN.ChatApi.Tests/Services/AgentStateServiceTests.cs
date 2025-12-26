using ALAN.ChatApi.Services;
using ALAN.Shared.Models;
using ALAN.Shared.Services.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace ALAN.ChatApi.Tests.Services;

public class AgentStateServiceTests
{
    private readonly Mock<ILogger<AgentStateService>> _loggerMock;
    private readonly Mock<IShortTermMemoryService> _shortTermMemoryMock;
    private readonly Mock<ILongTermMemoryService> _longTermMemoryMock;

    public AgentStateServiceTests()
    {
        _loggerMock = new Mock<ILogger<AgentStateService>>();
        _shortTermMemoryMock = new Mock<IShortTermMemoryService>();
        _longTermMemoryMock = new Mock<ILongTermMemoryService>();
    }

    [Fact]
    public void GetCurrentState_ReturnsInitialState()
    {
        // Arrange
        var service = new AgentStateService(
            _loggerMock.Object,
            _shortTermMemoryMock.Object,
            _longTermMemoryMock.Object);

        // Act
        var state = service.GetCurrentState();

        // Assert
        Assert.NotNull(state);
        Assert.Equal(AgentStatus.Idle, state.Status);
    }

    [Fact]
    public void GetCurrentState_ReturnsConsistentState()
    {
        // Arrange
        var service = new AgentStateService(
            _loggerMock.Object,
            _shortTermMemoryMock.Object,
            _longTermMemoryMock.Object);

        // Act
        var state1 = service.GetCurrentState();
        var state2 = service.GetCurrentState();

        // Assert
        Assert.Same(state1, state2); // Should return the same instance
    }
}
