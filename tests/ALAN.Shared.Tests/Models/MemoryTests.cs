using ALAN.Shared.Models;

namespace ALAN.Shared.Tests.Models;

public class MemoryEntryTests
{
    [Fact]
    public void MemoryEntry_DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var memory = new MemoryEntry();

        // Assert
        Assert.NotNull(memory.Id);
        Assert.NotEqual(Guid.Empty.ToString(), memory.Id);
        Assert.True(memory.Timestamp <= DateTime.UtcNow);
        Assert.Equal(MemoryType.Observation, memory.Type);
        Assert.Equal(string.Empty, memory.Content);
        Assert.Equal(string.Empty, memory.Summary);
        Assert.NotNull(memory.Metadata);
        Assert.Empty(memory.Metadata);
        Assert.Equal(0, memory.AccessCount);
        Assert.True(memory.LastAccessed <= DateTime.UtcNow);
        Assert.Equal(0.5, memory.Importance);
        Assert.NotNull(memory.Tags);
        Assert.Empty(memory.Tags);
        Assert.Null(memory.EmbeddingId);
    }

    [Theory]
    [InlineData(MemoryType.Observation)]
    [InlineData(MemoryType.Learning)]
    [InlineData(MemoryType.CodeChange)]
    [InlineData(MemoryType.Decision)]
    [InlineData(MemoryType.Reflection)]
    [InlineData(MemoryType.Error)]
    [InlineData(MemoryType.Success)]
    public void MemoryEntry_Type_CanBeSetToAnyValue(MemoryType type)
    {
        // Arrange
        var memory = new MemoryEntry();

        // Act
        memory.Type = type;

        // Assert
        Assert.Equal(type, memory.Type);
    }

    [Fact]
    public void MemoryEntry_Properties_CanBeSet()
    {
        // Arrange
        var memory = new MemoryEntry();

        // Act
        memory.Content = "Test content";
        memory.Summary = "Test summary";
        memory.Importance = 0.8;
        memory.AccessCount = 5;
        memory.EmbeddingId = "emb_123";

        // Assert
        Assert.Equal("Test content", memory.Content);
        Assert.Equal("Test summary", memory.Summary);
        Assert.Equal(0.8, memory.Importance);
        Assert.Equal(5, memory.AccessCount);
        Assert.Equal("emb_123", memory.EmbeddingId);
    }

    [Fact]
    public void MemoryEntry_Metadata_CanBePopulated()
    {
        // Arrange
        var memory = new MemoryEntry();
        var metadata = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };

        // Act
        memory.Metadata = metadata;

        // Assert
        Assert.Equal(2, memory.Metadata.Count);
        Assert.Equal("value1", memory.Metadata["key1"]);
        Assert.Equal("value2", memory.Metadata["key2"]);
    }

    [Fact]
    public void MemoryEntry_Tags_CanBePopulated()
    {
        // Arrange
        var memory = new MemoryEntry();
        var tags = new List<string> { "tag1", "tag2", "tag3" };

        // Act
        memory.Tags = tags;

        // Assert
        Assert.Equal(3, memory.Tags.Count);
        Assert.Contains("tag1", memory.Tags);
        Assert.Contains("tag2", memory.Tags);
        Assert.Contains("tag3", memory.Tags);
    }
}

public class ConsolidatedLearningTests
{
    [Fact]
    public void ConsolidatedLearning_DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var learning = new ConsolidatedLearning();

        // Assert
        Assert.NotNull(learning.Id);
        Assert.NotEqual(Guid.Empty.ToString(), learning.Id);
        Assert.True(learning.Created <= DateTime.UtcNow);
        Assert.Equal(string.Empty, learning.Topic);
        Assert.Equal(string.Empty, learning.Summary);
        Assert.NotNull(learning.SourceMemoryIds);
        Assert.Empty(learning.SourceMemoryIds);
        Assert.NotNull(learning.Insights);
        Assert.Empty(learning.Insights);
        Assert.Equal(0.7, learning.Confidence);
    }

    [Fact]
    public void ConsolidatedLearning_Properties_CanBeSet()
    {
        // Arrange
        var learning = new ConsolidatedLearning();

        // Act
        learning.Topic = "Testing Patterns";
        learning.Summary = "Learned about testing patterns";
        learning.Confidence = 0.9;
        learning.SourceMemoryIds = new List<string> { "mem1", "mem2" };
        learning.Insights = new Dictionary<string, object>
        {
            ["key"] = "value",
            ["count"] = 42
        };

        // Assert
        Assert.Equal("Testing Patterns", learning.Topic);
        Assert.Equal("Learned about testing patterns", learning.Summary);
        Assert.Equal(0.9, learning.Confidence);
        Assert.Equal(2, learning.SourceMemoryIds.Count);
        Assert.Equal(2, learning.Insights.Count);
    }
}
