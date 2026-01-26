using Microsoft.Extensions.Logging;
using Microsoft.Agents.AI;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text.RegularExpressions;

namespace ALAN.Agent.Services.MCP;

/// <summary>
/// Service to configure MCP (Model Context Protocol) tools for the AI Agent.
/// Reads configuration from YAML and sets up MCP server connections.
/// Supports both HTTP and stdio (npx/command) transports.
/// </summary>
public partial class McpConfigurationService : IAsyncDisposable
{
    private readonly ILogger<McpConfigurationService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly List<McpClient> _mcpClients = [];
    private bool _disposed;

    public McpConfigurationService(ILogger<McpConfigurationService> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    [GeneratedRegex(@"\$\{([^}]+)\}|\$([A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex EnvVarPattern();

    /// <summary>
    /// Resolves environment variable placeholders in a string.
    /// Supports both ${VAR_NAME} and $VAR_NAME syntax.
    /// </summary>
    private static string? ResolveEnvironmentVariables(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return EnvVarPattern().Replace(value, match =>
        {
            // Group 1: ${VAR_NAME} format, Group 2: $VAR_NAME format
            string varName = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            return Environment.GetEnvironmentVariable(varName) ?? match.Value;
        });
    }

    /// <summary>
    /// Disposes all MCP clients and their underlying processes.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var client in _mcpClients)
        {
            try
            {
                await client.DisposeAsync();
                _logger.LogDebug("Disposed MCP client");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing MCP client");
            }
        }

        _mcpClients.Clear();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Load MCP configuration from YAML file and configure the agent with MCP tools.
    /// </summary>
    public async Task<List<AITool>?> ConfigureMcpTools(string configPath)
    {
        if (!File.Exists(configPath))
        {
            _logger.LogWarning("MCP configuration file not found at {ConfigPath}", configPath);
            return null;
        }

        try
        {
            _logger.LogInformation("Loading MCP configuration from {ConfigPath}", configPath);

            var yamlContent = File.ReadAllText(configPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var config = deserializer.Deserialize<McpConfig>(yamlContent);

            if (config?.Mcp?.Servers == null)
            {
                _logger.LogWarning("No MCP servers configured in {ConfigPath}", configPath);
                return null;
            }

            List<AITool> tools = [];
            foreach (var server in config.Mcp.Servers)
            {
                try
                {
                    _logger.LogInformation("Configuring MCP server: {ServerName}", server.Key);

                    McpClient? mcpClient = null;

                    // Check if this is an HTTP or stdio transport
                    if (server.Value.Type == "http" && !string.IsNullOrEmpty(server.Value.Url))
                    {
                        mcpClient = await ConfigureHttpTransportAsync(server.Key, server.Value);
                    }
                    else if (!string.IsNullOrEmpty(server.Value.Command))
                    {
                        mcpClient = await ConfigureStdioTransportAsync(server.Key, server.Value);
                    }
                    else
                    {
                        _logger.LogWarning("  ⚠ Server '{ServerName}' has no valid transport configuration (no HTTP URL or command)", server.Key);
                        continue;
                    }

                    if (mcpClient != null)
                    {
                        _mcpClients.Add(mcpClient);
                        var toolsFromServer = await mcpClient.ListToolsAsync();
                        tools.AddRange(toolsFromServer);

                        _logger.LogDebug("  ✓ Retrieved {ToolCount} tools from server '{ServerName}'",
                            toolsFromServer.Count, server.Key);
                        _logger.LogDebug("  Tools: {ToolNames}",
                            string.Join(", ", toolsFromServer.Select(t => t.Name)));
                        _logger.LogInformation("  ✓ MCP server '{ServerName}' connected successfully", server.Key);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "  ✗ Failed to configure MCP server '{ServerName}'", server.Key);
                }
            }

            if (tools.Count == 0)
            {
                _logger.LogWarning("⚠ No MCP tools were created! Check server configurations.");
            }
            else
            {
                _logger.LogInformation("✓ MCP configuration loaded successfully: {ToolCount} tools from {ServerCount} servers",
                    tools.Count, config.Mcp.Servers.Count);
            }

            return tools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load MCP configuration from {ConfigPath}", configPath);
        }
        return null;
    }

    /// <summary>
    /// Configures an HTTP transport for an MCP server.
    /// </summary>
    private async Task<McpClient?> ConfigureHttpTransportAsync(string serverName, McpServerConfig config)
    {
        _logger.LogInformation("  Type: HTTP");
        _logger.LogInformation("  URL: {Url}", config.Url);

        string? patToken = null;
        if (!string.IsNullOrEmpty(config.Pat))
        {
            patToken = ResolveEnvironmentVariables(config.Pat);
            if (string.IsNullOrEmpty(patToken) || patToken == config.Pat)
            {
                _logger.LogWarning("  ⚠ PAT value '{Pat}' could not be resolved for server '{ServerName}'",
                    config.Pat, serverName);
            }
        }

        var clientTransport = new HttpClientTransport(
            new()
            {
                Endpoint = new Uri(config.Url!),
                AdditionalHeaders = new Dictionary<string, string>
                {
                    { "User-Agent", "ALAN.Agent/MCPClient" },
                    { "Authorization", !string.IsNullOrEmpty(patToken) ? $"Bearer {patToken}" : string.Empty }
                }
            }
        );

        return await McpClient.CreateAsync(clientTransport, loggerFactory: _loggerFactory);
    }

    /// <summary>
    /// Configures a stdio transport for an MCP server (npx, command-based servers).
    /// </summary>
    private async Task<McpClient?> ConfigureStdioTransportAsync(string serverName, McpServerConfig config)
    {
        _logger.LogInformation("  Type: stdio (command-based)");
        _logger.LogInformation("  Command: {Command}", config.Command);
        _logger.LogInformation("  Args: {Args}", string.Join(" ", config.Args ?? []));

        // Build environment variables dictionary
        Dictionary<string, string?>? envVars = null;
        if (config.Env != null && config.Env.Count > 0)
        {
            envVars = [];
            foreach (var env in config.Env)
            {
                var resolvedValue = ResolveEnvironmentVariables(env.Value);
                envVars[env.Key] = resolvedValue;

                if (resolvedValue != env.Value)
                {
                    _logger.LogDebug("  Env {Key}: (resolved from environment)", env.Key);
                }
                else
                {
                    _logger.LogDebug("  Env {Key}: {Value}", env.Key, 
                        env.Key.Contains("TOKEN", StringComparison.OrdinalIgnoreCase) || 
                        env.Key.Contains("KEY", StringComparison.OrdinalIgnoreCase) ||
                        env.Key.Contains("SECRET", StringComparison.OrdinalIgnoreCase)
                            ? "***" : resolvedValue);
                }
            }
        }

        var transportOptions = new StdioClientTransportOptions
        {
            Name = serverName,
            Command = config.Command,
            Arguments = config.Args?.ToList(),
            EnvironmentVariables = envVars,
            StandardErrorLines = line => _logger.LogDebug("[{ServerName}] stderr: {Line}", serverName, line)
        };

        var transport = new StdioClientTransport(transportOptions, _loggerFactory);

        return await McpClient.CreateAsync(transport, loggerFactory: _loggerFactory);
    }
}

public class McpConfig
{
    public McpSection? Mcp { get; set; }
}

public class McpSection
{
    public Dictionary<string, McpServerConfig>? Servers { get; set; }
}

public class McpServerConfig
{
    public string Command { get; set; } = string.Empty;
    public List<string>? Args { get; set; }
    public Dictionary<string, string>? Env { get; set; }

    public string? Type { get; set; }
    public string? Url { get; set; }
    public string? Pat { get; set; }

    public string? Description { get; set; }
}
