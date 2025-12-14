using ALAN.ChatApi.Controllers;
using ALAN.Shared.Models;
using ALAN.Shared.Services.Queue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace ALAN.ChatApi.Tests.Controllers;

public class HumanInputControllerTests
{
    private readonly Mock<ILogger<HumanInputController>> _loggerMock;
    private readonly Mock<IMessageQueue<HumanInput>> _queueMock;

    public HumanInputControllerTests()
    {
        _loggerMock = new Mock<ILogger<HumanInputController>>();
        _queueMock = new Mock<IMessageQueue<HumanInput>>();
    }

    [Fact]
    public async Task PauseAgent_SendsPauseCommand_ReturnsOk()
    {
        // Arrange
        var controller = new HumanInputController(_loggerMock.Object, _queueMock.Object);

        // Act
        var result = await controller.PauseAgent(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        _queueMock.Verify(q => q.SendAsync(
            It.Is<HumanInput>(hi => hi.Type == HumanInputType.PauseAgent),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResumeAgent_SendsResumeCommand_ReturnsOk()
    {
        // Arrange
        var controller = new HumanInputController(_loggerMock.Object, _queueMock.Object);

        // Act
        var result = await controller.ResumeAgent(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        _queueMock.Verify(q => q.SendAsync(
            It.Is<HumanInput>(hi => hi.Type == HumanInputType.ResumeAgent),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdatePrompt_WithValidPrompt_SendsPromptCommand()
    {
        // Arrange
        var controller = new HumanInputController(_loggerMock.Object, _queueMock.Object);
        var promptRequest = new UpdatePromptRequest("New test prompt");

        // Act
        var result = await controller.UpdatePrompt(promptRequest, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        _queueMock.Verify(q => q.SendAsync(
            It.Is<HumanInput>(hi => 
                hi.Type == HumanInputType.UpdatePrompt && 
                hi.Content == "New test prompt"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdatePrompt_WithEmptyPrompt_ReturnsBadRequest()
    {
        // Arrange
        var controller = new HumanInputController(_loggerMock.Object, _queueMock.Object);
        var promptRequest = new UpdatePromptRequest("");

        // Act
        var result = await controller.UpdatePrompt(promptRequest, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        _queueMock.Verify(q => q.SendAsync(
            It.IsAny<HumanInput>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TriggerBatchLearning_SendsCommand_ReturnsOk()
    {
        // Arrange
        var controller = new HumanInputController(_loggerMock.Object, _queueMock.Object);

        // Act
        var result = await controller.TriggerBatchLearning(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        _queueMock.Verify(q => q.SendAsync(
            It.Is<HumanInput>(hi => hi.Type == HumanInputType.TriggerBatchLearning),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TriggerMemoryConsolidation_SendsCommand_ReturnsOk()
    {
        // Arrange
        var controller = new HumanInputController(_loggerMock.Object, _queueMock.Object);

        // Act
        var result = await controller.TriggerMemoryConsolidation(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        _queueMock.Verify(q => q.SendAsync(
            It.Is<HumanInput>(hi => hi.Type == HumanInputType.TriggerMemoryConsolidation),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitInput_WithValidInput_ReturnsOk()
    {
        // Arrange
        var controller = new HumanInputController(_loggerMock.Object, _queueMock.Object);
        var input = new HumanInput
        {
            Type = HumanInputType.UpdatePrompt,
            Content = "Test input"
        };

        // Act
        var result = await controller.SubmitInput(input, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        _queueMock.Verify(q => q.SendAsync(input, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitInput_WithNullInput_ReturnsBadRequest()
    {
        // Arrange
        var controller = new HumanInputController(_loggerMock.Object, _queueMock.Object);

        // Act - Using default! to explicitly indicate null test case
        var result = await controller.SubmitInput(default(HumanInput)!, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        _queueMock.Verify(q => q.SendAsync(
            It.IsAny<HumanInput>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
