using ALAN.ChatApi.Controllers;
using ALAN.ChatApi.Services;
using ALAN.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ALAN.ChatApi.Tests.Controllers;

public class StateControllerTests
{
    [Fact]
    public void GetState_ReturnsOkResult_WithAgentState()
    {
        // Arrange - Create a real AgentStateService (it's lightweight with in-memory state)
        var mockLogger = Mock.Of<Microsoft.Extensions.Logging.ILogger<AgentStateService>>();
        var mockShortTermMemory = Mock.Of<ALAN.Shared.Services.Memory.IShortTermMemoryService>();
        var mockLongTermMemory = Mock.Of<ALAN.Shared.Services.Memory.ILongTermMemoryService>();

        var stateService = new AgentStateService(mockLogger, mockShortTermMemory, mockLongTermMemory);
        var controller = new StateController(stateService);

        // Act
        var result = controller.GetState();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedState = Assert.IsType<AgentState>(okResult.Value);
        Assert.NotNull(returnedState);
        Assert.Equal(AgentStatus.Idle, returnedState.Status); // Initial state
    }

    [Fact]
    public void GetState_ReturnsCurrentState_WhenCalled()
    {
        // Arrange
        var mockLogger = Mock.Of<Microsoft.Extensions.Logging.ILogger<AgentStateService>>();
        var mockShortTermMemory = Mock.Of<ALAN.Shared.Services.Memory.IShortTermMemoryService>();
        var mockLongTermMemory = Mock.Of<ALAN.Shared.Services.Memory.ILongTermMemoryService>();

        var stateService = new AgentStateService(mockLogger, mockShortTermMemory, mockLongTermMemory);
        var controller = new StateController(stateService);

        // Act
        var result1 = controller.GetState();
        var result2 = controller.GetState();

        // Assert
        var okResult1 = Assert.IsType<OkObjectResult>(result1);
        
        var state1 = Assert.IsType<AgentState>(okResult1.Value);
        var state2 = Assert.IsType<OkObjectResult>(result2).Value as AgentState;
        
        // Both calls should return the same state instance
        Assert.Same(state1, state2);
    }
}
