using ALAN.Agent.Services;
using ALAN.Shared.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ALAN.Agent.Tests.Services;

public class PromptServiceTests : IDisposable
{
    private readonly Mock<ILogger<PromptService>> _mockLogger;
    private readonly string _testPromptsDirectory;

    public PromptServiceTests()
    {
        _mockLogger = new Mock<ILogger<PromptService>>();
        _testPromptsDirectory = Path.Combine(Path.GetTempPath(), $"alan-test-prompts-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testPromptsDirectory);
    }

    [Fact]
    public void Constructor_CreatesPromptsDirectory_WhenNotExists()
    {
        // Arrange
        var nonExistentDir = Path.Combine(Path.GetTempPath(), $"alan-test-{Guid.NewGuid()}");

        // Act
        var service = new PromptService(_mockLogger.Object, nonExistentDir);

        // Assert
        Assert.True(Directory.Exists(nonExistentDir));
        
        // Verify the service can list templates (even if empty)
        var templates = service.GetAvailableTemplates();
        Assert.NotNull(templates);

        // Cleanup
        Directory.Delete(nonExistentDir);
    }

    [Fact]
    public void RenderTemplate_WithSimpleTemplate_ReturnsRenderedText()
    {
        // Arrange
        var templatePath = Path.Combine(_testPromptsDirectory, "simple.hbs");
        File.WriteAllText(templatePath, "Hello {{name}}!");
        
        var service = new PromptService(_mockLogger.Object, _testPromptsDirectory);

        // Act
        var result = service.RenderTemplate("simple", new { name = "World" });

        // Assert
        Assert.Equal("Hello World!", result);
    }

    [Fact]
    public void RenderTemplate_WithComplexTemplate_ReturnsRenderedText()
    {
        // Arrange
        var templatePath = Path.Combine(_testPromptsDirectory, "complex.hbs");
        File.WriteAllText(templatePath, @"Count: {{count}}
{{#if hasItems}}
Items exist
{{else}}
No items
{{/if}}");
        
        var service = new PromptService(_mockLogger.Object, _testPromptsDirectory);

        // Act
        var result = service.RenderTemplate("complex", new { count = 5, hasItems = true });

        // Assert
        Assert.Contains("Count: 5", result);
        Assert.Contains("Items exist", result);
    }

    [Fact]
    public void RenderTemplate_ThrowsException_WhenTemplateNotFound()
    {
        // Arrange
        var service = new PromptService(_mockLogger.Object, _testPromptsDirectory);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            service.RenderTemplate("nonexistent", new { }));
        
        Assert.Contains("Failed to render template 'nonexistent'", exception.Message);
    }

    [Fact]
    public void RenderTemplate_CachesTemplate_OnSecondCall()
    {
        // Arrange
        var templatePath = Path.Combine(_testPromptsDirectory, "cached.hbs");
        File.WriteAllText(templatePath, "Cached: {{value}}");
        
        var service = new PromptService(_mockLogger.Object, _testPromptsDirectory);

        // Act
        var result1 = service.RenderTemplate("cached", new { value = "first" });
        
        // Modify the file to verify it uses cached version
        File.WriteAllText(templatePath, "Modified: {{value}}");
        var result2 = service.RenderTemplate("cached", new { value = "second" });

        // Assert
        Assert.Equal("Cached: first", result1);
        Assert.Equal("Cached: second", result2); // Should use cached template
    }

    [Fact]
    public void ClearCache_RemovesAllCachedTemplates()
    {
        // Arrange
        var templatePath = Path.Combine(_testPromptsDirectory, "clearable.hbs");
        File.WriteAllText(templatePath, "Original: {{value}}");
        
        var service = new PromptService(_mockLogger.Object, _testPromptsDirectory);
        service.RenderTemplate("clearable", new { value = "first" });
        
        // Modify template and clear cache
        File.WriteAllText(templatePath, "Modified: {{value}}");
        service.ClearCache();

        // Act
        var result = service.RenderTemplate("clearable", new { value = "test" });

        // Assert
        Assert.Equal("Modified: test", result); // Should use new template after cache clear
    }

    [Fact]
    public void ReloadTemplate_RemovesSpecificTemplate_FromCache()
    {
        // Arrange
        var templatePath = Path.Combine(_testPromptsDirectory, "reloadable.hbs");
        File.WriteAllText(templatePath, "Original: {{value}}");
        
        var service = new PromptService(_mockLogger.Object, _testPromptsDirectory);
        service.RenderTemplate("reloadable", new { value = "first" });
        
        // Modify template and reload it
        File.WriteAllText(templatePath, "Reloaded: {{value}}");
        service.ReloadTemplate("reloadable");

        // Act
        var result = service.RenderTemplate("reloadable", new { value = "test" });

        // Assert
        Assert.Equal("Reloaded: test", result);
    }

    [Fact]
    public void GetAvailableTemplates_ReturnsListOfTemplates()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testPromptsDirectory, "template1.hbs"), "Template 1");
        File.WriteAllText(Path.Combine(_testPromptsDirectory, "template2.hbs"), "Template 2");
        File.WriteAllText(Path.Combine(_testPromptsDirectory, "template3.hbs"), "Template 3");
        
        var service = new PromptService(_mockLogger.Object, _testPromptsDirectory);

        // Act
        var templates = service.GetAvailableTemplates();

        // Assert
        Assert.Equal(3, templates.Count);
        Assert.Contains("template1", templates);
        Assert.Contains("template2", templates);
        Assert.Contains("template3", templates);
    }

    [Fact]
    public void GetAvailableTemplates_ReturnsEmptyList_WhenNoTemplates()
    {
        // Arrange
        var emptyDir = Path.Combine(Path.GetTempPath(), $"alan-empty-{Guid.NewGuid()}");
        Directory.CreateDirectory(emptyDir);
        var service = new PromptService(_mockLogger.Object, emptyDir);

        // Act
        var templates = service.GetAvailableTemplates();

        // Assert
        Assert.Empty(templates);

        // Cleanup
        Directory.Delete(emptyDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testPromptsDirectory))
        {
            try
            {
                Directory.Delete(_testPromptsDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
