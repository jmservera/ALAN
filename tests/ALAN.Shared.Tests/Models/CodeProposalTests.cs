using ALAN.Shared.Models;

namespace ALAN.Shared.Tests.Models;

public class CodeProposalTests
{
    [Fact]
    public void CodeProposal_DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var proposal = new CodeProposal();

        // Assert
        Assert.NotNull(proposal.Id);
        Assert.NotEqual(Guid.Empty.ToString(), proposal.Id);
        Assert.True(proposal.Created <= DateTime.UtcNow);
        Assert.Equal(string.Empty, proposal.Title);
        Assert.Equal(string.Empty, proposal.Description);
        Assert.Equal(string.Empty, proposal.Reasoning);
        Assert.NotNull(proposal.FileChanges);
        Assert.Empty(proposal.FileChanges);
        Assert.Equal(ProposalStatus.Pending, proposal.Status);
        Assert.Null(proposal.ApprovedBy);
        Assert.Null(proposal.ApprovedAt);
        Assert.Null(proposal.RejectionReason);
        Assert.Null(proposal.BranchName);
        Assert.Null(proposal.PullRequestUrl);
    }

    [Theory]
    [InlineData(ProposalStatus.Pending)]
    [InlineData(ProposalStatus.Approved)]
    [InlineData(ProposalStatus.Rejected)]
    [InlineData(ProposalStatus.Implemented)]
    [InlineData(ProposalStatus.Failed)]
    public void CodeProposal_Status_CanBeSetToAnyValue(ProposalStatus status)
    {
        // Arrange
        var proposal = new CodeProposal();

        // Act
        proposal.Status = status;

        // Assert
        Assert.Equal(status, proposal.Status);
    }

    [Fact]
    public void CodeProposal_Properties_CanBeSet()
    {
        // Arrange
        var proposal = new CodeProposal();

        // Act
        proposal.Title = "Add new feature";
        proposal.Description = "This feature does X";
        proposal.Reasoning = "We need this because Y";
        proposal.ApprovedBy = "admin";
        proposal.ApprovedAt = DateTime.UtcNow;
        proposal.BranchName = "feature/test";
        proposal.PullRequestUrl = "https://github.com/test/pr/1";

        // Assert
        Assert.Equal("Add new feature", proposal.Title);
        Assert.Equal("This feature does X", proposal.Description);
        Assert.Equal("We need this because Y", proposal.Reasoning);
        Assert.Equal("admin", proposal.ApprovedBy);
        Assert.NotNull(proposal.ApprovedAt);
        Assert.Equal("feature/test", proposal.BranchName);
        Assert.Equal("https://github.com/test/pr/1", proposal.PullRequestUrl);
    }

    [Fact]
    public void CodeProposal_FileChanges_CanBePopulated()
    {
        // Arrange
        var proposal = new CodeProposal();
        var fileChanges = new List<FileChange>
        {
            new()
            {
                FilePath = "/src/file1.cs",
                Type = ChangeType.Add,
                NewContent = "new content"
            },
            new()
            {
                FilePath = "/src/file2.cs",
                Type = ChangeType.Modify,
                OriginalContent = "old",
                NewContent = "new"
            }
        };

        // Act
        proposal.FileChanges = fileChanges;

        // Assert
        Assert.Equal(2, proposal.FileChanges.Count);
        Assert.Contains(proposal.FileChanges, fc => fc.Type == ChangeType.Add);
        Assert.Contains(proposal.FileChanges, fc => fc.Type == ChangeType.Modify);
    }
}

public class FileChangeTests
{
    [Fact]
    public void FileChange_DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var fileChange = new FileChange();

        // Assert
        Assert.Equal(string.Empty, fileChange.FilePath);
        Assert.Equal(ChangeType.Add, fileChange.Type);
        Assert.Null(fileChange.OriginalContent);
        Assert.Equal(string.Empty, fileChange.NewContent);
        Assert.Equal(string.Empty, fileChange.Diff);
    }

    [Theory]
    [InlineData(ChangeType.Add)]
    [InlineData(ChangeType.Modify)]
    [InlineData(ChangeType.Delete)]
    public void FileChange_Type_CanBeSetToAnyValue(ChangeType type)
    {
        // Arrange
        var fileChange = new FileChange();

        // Act
        fileChange.Type = type;

        // Assert
        Assert.Equal(type, fileChange.Type);
    }

    [Fact]
    public void FileChange_Properties_CanBeSet()
    {
        // Arrange
        var fileChange = new FileChange();

        // Act
        fileChange.FilePath = "/src/test.cs";
        fileChange.OriginalContent = "original";
        fileChange.NewContent = "modified";
        fileChange.Diff = "+modified\n-original";

        // Assert
        Assert.Equal("/src/test.cs", fileChange.FilePath);
        Assert.Equal("original", fileChange.OriginalContent);
        Assert.Equal("modified", fileChange.NewContent);
        Assert.Equal("+modified\n-original", fileChange.Diff);
    }
}
