using ALAN.Agent.Services;
using ALAN.Shared.Models;
using ALAN.Shared.Services.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace ALAN.Agent.Tests.Services;

public class CodeProposalServiceTests
{
    private readonly Mock<ILongTermMemoryService> _mockLongTermMemory;
    private readonly Mock<ILogger<CodeProposalService>> _mockLogger;
    private readonly CodeProposalService _service;

    public CodeProposalServiceTests()
    {
        _mockLongTermMemory = new Mock<ILongTermMemoryService>();
        _mockLogger = new Mock<ILogger<CodeProposalService>>();
        _service = new CodeProposalService(_mockLongTermMemory.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task CreateProposalAsync_StoresProposal()
    {
        // Arrange
        var proposal = new CodeProposal
        {
            Title = "Test Proposal",
            Description = "Test Description",
            Reasoning = "Test Reasoning"
        };

        // Act
        var proposalId = await _service.CreateProposalAsync(proposal);

        // Assert
        Assert.Equal(proposal.Id, proposalId);
        var retrieved = _service.GetProposal(proposalId);
        Assert.NotNull(retrieved);
        Assert.Equal("Test Proposal", retrieved.Title);
    }

    [Fact]
    public async Task CreateProposalAsync_StoresInLongTermMemory()
    {
        // Arrange
        var proposal = new CodeProposal
        {
            Title = "Test Proposal",
            Description = "Test Description"
        };

        // Act
        await _service.CreateProposalAsync(proposal);

        // Assert
        _mockLongTermMemory.Verify(
            m => m.StoreMemoryAsync(
                It.Is<MemoryEntry>(me => 
                    me.Type == MemoryType.CodeChange && 
                    me.Tags.Contains("code-proposal")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void GetProposal_ReturnsNullForNonExistentId()
    {
        // Act
        var proposal = _service.GetProposal("non-existent-id");

        // Assert
        Assert.Null(proposal);
    }

    [Fact]
    public async Task GetPendingProposals_ReturnsOnlyPendingProposals()
    {
        // Arrange
        await _service.CreateProposalAsync(new CodeProposal 
        { 
            Title = "Pending 1",
            Status = ProposalStatus.Pending 
        });
        await _service.CreateProposalAsync(new CodeProposal 
        { 
            Title = "Approved 1",
            Status = ProposalStatus.Approved 
        });
        await _service.CreateProposalAsync(new CodeProposal 
        { 
            Title = "Pending 2",
            Status = ProposalStatus.Pending 
        });

        // Act
        var pending = _service.GetPendingProposals();

        // Assert
        Assert.Equal(2, pending.Count);
        Assert.All(pending, p => Assert.Equal(ProposalStatus.Pending, p.Status));
    }

    [Fact]
    public async Task ApproveProposalAsync_ApprovesProposal()
    {
        // Arrange
        var proposal = new CodeProposal { Title = "Test" };
        var id = await _service.CreateProposalAsync(proposal);

        // Act
        var result = await _service.ApproveProposalAsync(id, "testuser");

        // Assert
        Assert.True(result);
        var approved = _service.GetProposal(id);
        Assert.NotNull(approved);
        Assert.Equal(ProposalStatus.Approved, approved.Status);
        Assert.Equal("testuser", approved.ApprovedBy);
        Assert.NotNull(approved.ApprovedAt);
    }

    [Fact]
    public async Task ApproveProposalAsync_StoresApprovalInMemory()
    {
        // Arrange
        var proposal = new CodeProposal { Title = "Test" };
        var id = await _service.CreateProposalAsync(proposal);

        // Act
        await _service.ApproveProposalAsync(id, "testuser");

        // Assert
        _mockLongTermMemory.Verify(
            m => m.StoreMemoryAsync(
                It.Is<MemoryEntry>(me => 
                    me.Type == MemoryType.CodeChange && 
                    me.Tags.Contains("approved")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ApproveProposalAsync_ReturnsFalseForNonExistentProposal()
    {
        // Act
        var result = await _service.ApproveProposalAsync("non-existent", "user");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ApproveProposalAsync_ReturnsFalseForNonPendingProposal()
    {
        // Arrange
        var proposal = new CodeProposal { Title = "Test", Status = ProposalStatus.Approved };
        var id = await _service.CreateProposalAsync(proposal);

        // Act
        var result = await _service.ApproveProposalAsync(id, "user");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RejectProposalAsync_RejectsProposal()
    {
        // Arrange
        var proposal = new CodeProposal { Title = "Test" };
        var id = await _service.CreateProposalAsync(proposal);

        // Act
        var result = await _service.RejectProposalAsync(id, "Not needed");

        // Assert
        Assert.True(result);
        var rejected = _service.GetProposal(id);
        Assert.NotNull(rejected);
        Assert.Equal(ProposalStatus.Rejected, rejected.Status);
        Assert.Equal("Not needed", rejected.RejectionReason);
    }

    [Fact]
    public async Task MarkAsImplementedAsync_MarksProposalAsImplemented()
    {
        // Arrange
        var proposal = new CodeProposal { Title = "Test" };
        var id = await _service.CreateProposalAsync(proposal);
        await _service.ApproveProposalAsync(id, "user");

        // Act
        var result = await _service.MarkAsImplementedAsync(id, "https://github.com/test/pr/1");

        // Assert
        Assert.True(result);
        var implemented = _service.GetProposal(id);
        Assert.NotNull(implemented);
        Assert.Equal(ProposalStatus.Implemented, implemented.Status);
        Assert.Equal("https://github.com/test/pr/1", implemented.PullRequestUrl);
    }

    [Fact]
    public async Task MarkAsImplementedAsync_ReturnsFalseForNonApprovedProposal()
    {
        // Arrange
        var proposal = new CodeProposal { Title = "Test" };
        var id = await _service.CreateProposalAsync(proposal);

        // Act
        var result = await _service.MarkAsImplementedAsync(id, "https://github.com/test/pr/1");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetStatistics_ReturnsCorrectCounts()
    {
        // Arrange
        await _service.CreateProposalAsync(new CodeProposal { Status = ProposalStatus.Pending });
        await _service.CreateProposalAsync(new CodeProposal { Status = ProposalStatus.Approved });
        await _service.CreateProposalAsync(new CodeProposal { Status = ProposalStatus.Rejected });
        await _service.CreateProposalAsync(new CodeProposal { Status = ProposalStatus.Implemented });

        // Act
        var stats = _service.GetStatistics();

        // Assert
        Assert.Equal(4, stats.Total);
        Assert.Equal(1, stats.Pending);
        Assert.Equal(1, stats.Approved);
        Assert.Equal(1, stats.Rejected);
        Assert.Equal(1, stats.Implemented);
        Assert.Equal(0, stats.Failed);
    }

    [Fact]
    public async Task GetProposalsByStatus_ReturnsOnlyMatchingStatus()
    {
        // Arrange
        await _service.CreateProposalAsync(new CodeProposal { Status = ProposalStatus.Approved });
        await _service.CreateProposalAsync(new CodeProposal { Status = ProposalStatus.Rejected });
        await _service.CreateProposalAsync(new CodeProposal { Status = ProposalStatus.Approved });

        // Act
        var approved = _service.GetProposalsByStatus(ProposalStatus.Approved);

        // Assert
        Assert.Equal(2, approved.Count);
        Assert.All(approved, p => Assert.Equal(ProposalStatus.Approved, p.Status));
    }
}
