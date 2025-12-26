using Microsoft.Extensions.Logging;
using Microsoft.Agents.AI;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace ALAN.Agent.Services.MCP;

/// <summary>
/// Service to configure MCP (Model Context Protocol) tools for the AI Agent.
/// Reads configuration from YAML and sets up MCP server connections.
/// </summary>
public class McpConfigurationService
{
    private readonly ILogger<McpConfigurationService> _logger;

    public McpConfigurationService(ILogger<McpConfigurationService> logger)
    {
        _logger = logger;
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
                try{
                    _logger.LogInformation("Configuring MCP server: {ServerName}", server.Key);
                    _logger.LogInformation("  Command: {Command}", server.Value.Command);
                    _logger.LogInformation("  Args: {Args}", string.Join(" ", server.Value.Args ?? []));

                    if (server.Value.Type == "http" && !string.IsNullOrEmpty(server.Value.Url))
                    {
                        _logger.LogInformation("  Type: {Type}", server.Value.Type);
                        _logger.LogInformation("  URL: {Url}", server.Value.Url);


                        string? patToken = null;
                        if (!string.IsNullOrEmpty(server.Value.Pat))
                        {
                            if (server.Value.Pat.StartsWith('$'))
                            {
                                var envVarName = server.Value.Pat[1..];
                                var patValue = Environment.GetEnvironmentVariable(envVarName);
                                if (!string.IsNullOrEmpty(patValue))
                                {
                                    patToken = patValue;
                                }
                                else
                                {
                                    _logger.LogWarning("  ⚠ PAT environment variable '{EnvVar}' not set for server '{ServerName}'",
                                        envVarName, server.Key);
                                }
                            }
                            else
                            {
                                patToken = server.Value.Pat;
                            }
                        }

                        var clientTransport = new HttpClientTransport(
                            new()
                            {
                                Endpoint = new Uri(server.Value.Url),
                                AdditionalHeaders = new Dictionary<string, string>
                                {
                                    { "User-Agent", "ALAN.Agent/MCPClient" },
                                    {"Authorization", !string.IsNullOrEmpty(patToken) ? $"Bearer {patToken}" : string.Empty }
                                }
                            }
                        );

                        var mcpClient = await McpClient.CreateAsync(clientTransport);
                        var toolsFromServer = await mcpClient.ListToolsAsync();

                        tools.AddRange(toolsFromServer);
                        _logger.LogDebug("  ✓ Retrieved {ToolCount} tools from server '{ServerName}'",
                            toolsFromServer.Count, server.Key);
                        _logger.LogDebug("  Tools: {ToolNames}",
                            string.Join(", ", toolsFromServer.Select(t => t.Name)));

                        _logger.LogInformation("  ✓ MCP tool '{ServerName}' added successfully", server.Key);
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
