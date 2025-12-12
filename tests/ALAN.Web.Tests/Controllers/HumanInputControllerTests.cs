using ALAN.Shared.Models;
using ALAN.Shared.Services.Queue;
using ALAN.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace ALAN.Web.Tests.Controllers;

public class HumanInputControllerTests
{
    [Fact]
    public async Task TriggerMemoryConsolidation_EnqueuesCommand()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<HumanInputController>>();
        var mockQueue = new Mock<IMessageQueue<HumanInput>>();
        var controller = new HumanInputController(mockLogger.Object, mockQueue.Object);

        mockQueue
            .Setup(q => q.SendAsync(It.IsAny<HumanInput>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await controller.TriggerMemoryConsolidation(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var messageProp = okResult.Value?.GetType().GetProperty("message")?.GetValue(okResult.Value)?.ToString();
        Assert.Equal("Memory consolidation trigger queued", messageProp);

        mockQueue.Verify(q => q.SendAsync(
            It.Is<HumanInput>(h => h.Type == HumanInputType.TriggerMemoryConsolidation),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
