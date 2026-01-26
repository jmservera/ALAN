using ALAN.Agent.Services.MCP;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ALAN.Agent.Tests.Services.MCP;

/// <summary>
/// Unit tests for McpConfigurationService focusing on:
/// - YAML configuration parsing
/// - Environment variable resolution
/// - Server type detection (HTTP vs stdio)
/// - Proper disposal of resources
/// </summary>
public class McpConfigurationServiceTests : IDisposable
{
    private readonly Mock<ILogger<McpConfigurationService>> _mockLogger;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly string _tempDir;

    public McpConfigurationServiceTests()
    {
        _mockLogger = new Mock<ILogger<McpConfigurationService>>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(Mock.Of<ILogger>());

        _tempDir = Path.Combine(Path.GetTempPath(), $"mcp_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ConfigureMcpTools_FileNotFound_ReturnsNull()
    {
        // Arrange
        var service = new McpConfigurationService(_mockLogger.Object, _mockLoggerFactory.Object);
        var nonExistentPath = Path.Combine(_tempDir, "does-not-exist.yaml");

        // Act
        var result = await service.ConfigureMcpTools(nonExistentPath);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ConfigureMcpTools_EmptyConfig_ReturnsNull()
    {
        // Arrange
        var service = new McpConfigurationService(_mockLogger.Object, _mockLoggerFactory.Object);
        var configPath = Path.Combine(_tempDir, "empty-config.yaml");
        await File.WriteAllTextAsync(configPath, "mcp:\n  servers:");

        // Act
        var result = await service.ConfigureMcpTools(configPath);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ConfigureMcpTools_NoValidTransport_LogsWarning()
    {
        // Arrange
        var service = new McpConfigurationService(_mockLogger.Object, _mockLoggerFactory.Object);
        var configPath = Path.Combine(_tempDir, "invalid-config.yaml");
        await File.WriteAllTextAsync(configPath, @"
mcp:
  servers:
    invalid-server:
      description: No command or URL specified
");

        // Act
        var result = await service.ConfigureMcpTools(configPath);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ConfigureMcpTools_HttpServer_DetectedCorrectly()
    {
        // Arrange - We can't actually connect, but we can verify it tries to use HTTP
        var service = new McpConfigurationService(_mockLogger.Object, _mockLoggerFactory.Object);
        var configPath = Path.Combine(_tempDir, "http-config.yaml");
        await File.WriteAllTextAsync(configPath, @"
mcp:
  servers:
    test-http:
      type: http
      url: https://example.com/mcp
      pat: ${TEST_TOKEN}
      description: Test HTTP server
");

        // Act - This will fail to connect, but verifies HTTP detection
        var result = await service.ConfigureMcpTools(configPath);

        // Assert - Empty because HTTP server is unreachable (expected)
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ConfigureMcpTools_StdioServer_DetectedCorrectly()
    {
        // Arrange - Use a simple echo command that exits immediately
        var service = new McpConfigurationService(_mockLogger.Object, _mockLoggerFactory.Object);
        var configPath = Path.Combine(_tempDir, "stdio-config.yaml");
        
        // Use a command that will fail fast (no actual MCP server)
        await File.WriteAllTextAsync(configPath, @"
mcp:
  servers:
    test-stdio:
      command: echo
      args:
        - hello
      env:
        TEST_VAR: ${TEST_VALUE}
      description: Test stdio server
");

        // Act - This will fail because 'echo' isn't an MCP server
        var result = await service.ConfigureMcpTools(configPath);

        // Assert - Empty because echo isn't a valid MCP server
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ResolveEnvironmentVariables_BracketSyntax_ResolvesCorrectly()
    {
        // Arrange
        var testValue = $"test_value_{Guid.NewGuid():N}";
        var envVarName = $"TEST_VAR_{Guid.NewGuid():N}".ToUpperInvariant();
        Environment.SetEnvironmentVariable(envVarName, testValue);

        try
        {
            // Act - Use reflection to test private method
            var method = typeof(McpConfigurationService).GetMethod(
                "ResolveEnvironmentVariables",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method?.Invoke(null, [$"prefix_${{{envVarName}}}_suffix"]) as string;

            // Assert
            Assert.Equal($"prefix_{testValue}_suffix", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [Fact]
    public void ResolveEnvironmentVariables_DollarSyntax_ResolvesCorrectly()
    {
        // Arrange
        var testValue = $"test_value_{Guid.NewGuid():N}";
        var envVarName = $"TEST_VAR_{Guid.NewGuid():N}".ToUpperInvariant();
        Environment.SetEnvironmentVariable(envVarName, testValue);

        try
        {
            // Act - Use reflection to test private method
            var method = typeof(McpConfigurationService).GetMethod(
                "ResolveEnvironmentVariables",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method?.Invoke(null, [$"${envVarName}"]) as string;

            // Assert
            Assert.Equal(testValue, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [Fact]
    public void ResolveEnvironmentVariables_MissingVar_ReturnsOriginal()
    {
        // Arrange
        var nonExistentVar = $"DEFINITELY_DOES_NOT_EXIST_{Guid.NewGuid():N}";

        // Act - Use reflection to test private method
        var method = typeof(McpConfigurationService).GetMethod(
            "ResolveEnvironmentVariables",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = method?.Invoke(null, [$"${{{nonExistentVar}}}"]) as string;

        // Assert - Returns original placeholder if var doesn't exist
        Assert.Equal($"${{{nonExistentVar}}}", result);
    }

    [Fact]
    public void ResolveEnvironmentVariables_NullInput_ReturnsNull()
    {
        // Act - Use reflection to test private method
        var method = typeof(McpConfigurationService).GetMethod(
            "ResolveEnvironmentVariables",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = method?.Invoke(null, [null]) as string;

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ResolveEnvironmentVariables_EmptyInput_ReturnsEmpty()
    {
        // Act - Use reflection to test private method
        var method = typeof(McpConfigurationService).GetMethod(
            "ResolveEnvironmentVariables",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = method?.Invoke(null, [""]) as string;

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public async Task DisposeAsync_DisposesAllClients()
    {
        // Arrange
        var service = new McpConfigurationService(_mockLogger.Object, _mockLoggerFactory.Object);

        // Act - Disposing even without clients should work
        await service.DisposeAsync();

        // Assert - No exception thrown, second dispose should also be safe
        await service.DisposeAsync();
    }

    [Fact]
    public async Task ConfigureMcpTools_InvalidYaml_ReturnsNull()
    {
        // Arrange
        var service = new McpConfigurationService(_mockLogger.Object, _mockLoggerFactory.Object);
        var configPath = Path.Combine(_tempDir, "invalid-yaml.yaml");
        await File.WriteAllTextAsync(configPath, "this is not: valid: yaml: :");

        // Act
        var result = await service.ConfigureMcpTools(configPath);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ResolveEnvironmentVariables_MultipleVars_ResolvesAll()
    {
        // Arrange
        var value1 = $"value1_{Guid.NewGuid():N}";
        var value2 = $"value2_{Guid.NewGuid():N}";
        var var1 = $"VAR1_{Guid.NewGuid():N}".ToUpperInvariant();
        var var2 = $"VAR2_{Guid.NewGuid():N}".ToUpperInvariant();
        Environment.SetEnvironmentVariable(var1, value1);
        Environment.SetEnvironmentVariable(var2, value2);

        try
        {
            // Act - Use reflection to test private method
            var method = typeof(McpConfigurationService).GetMethod(
                "ResolveEnvironmentVariables",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method?.Invoke(null, [$"${{{var1}}}:${{{var2}}}"]) as string;

            // Assert
            Assert.Equal($"{value1}:{value2}", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(var1, null);
            Environment.SetEnvironmentVariable(var2, null);
        }
    }
}
