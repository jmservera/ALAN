using ALAN.Agent.Services;
using ALAN.Agent.Services.MCP;
using ALAN.Shared.Services.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using Azure.AI.OpenAI;
using Azure;
using OpenAI;
using Azure.Identity;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Register services
builder.Services.AddSingleton<StateManager>();
builder.Services.AddSingleton<HumanInputHandler>();
builder.Services.AddSingleton<CodeProposalService>();
builder.Services.AddSingleton<McpConfigurationService>();

// Register memory services - Azure Storage is required
var storageConnectionString = builder.Configuration["AzureStorage:ConnectionString"]
    ?? builder.Configuration["AZURE_STORAGE_CONNECTION_STRING"]
    ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

if (string.IsNullOrEmpty(storageConnectionString))
{
    throw new InvalidOperationException("Azure Storage connection string is required. Set AZURE_STORAGE_CONNECTION_STRING environment variable or AzureStorage:ConnectionString in appsettings.json");
}

builder.Services.AddSingleton<ILongTermMemoryService>(sp =>
    new AzureBlobLongTermMemoryService(
        storageConnectionString,
        sp.GetRequiredService<ILogger<AzureBlobLongTermMemoryService>>()));

builder.Services.AddSingleton<IShortTermMemoryService>(sp =>
    new AzureBlobShortTermMemoryService(
        storageConnectionString,
        sp.GetRequiredService<ILogger<AzureBlobShortTermMemoryService>>()));

// Register consolidation service (requires AIAgent, so it's registered after)
builder.Services.AddSingleton<IMemoryConsolidationService, MemoryConsolidationService>();
builder.Services.AddSingleton<BatchLearningService>();

// Configure and register UsageTracker
var maxLoopsPerDay = int.TryParse(
    builder.Configuration["AGENT_MAX_LOOPS_PER_DAY"]
    ?? Environment.GetEnvironmentVariable("AGENT_MAX_LOOPS_PER_DAY")
    ?? "4000",
    out var loops) ? loops : 4000;

var maxTokensPerDay = int.TryParse(
    builder.Configuration["AGENT_MAX_TOKENS_PER_DAY"]
    ?? Environment.GetEnvironmentVariable("AGENT_MAX_TOKENS_PER_DAY")
    ?? "8000000",
    out var tokens) ? tokens : 8_000_000;

builder.Services.AddSingleton(sp =>
    new UsageTracker(
        sp.GetRequiredService<ILogger<UsageTracker>>(),
        maxLoopsPerDay,
        maxTokensPerDay));

// Try to get Azure OpenAI configuration
var endpoint = builder.Configuration["AzureOpenAI:Endpoint"]
    ?? builder.Configuration["AZURE_OPENAI_ENDPOINT"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");

var apiKey = builder.Configuration["AzureOpenAI:ApiKey"]
    ?? builder.Configuration["AZURE_OPENAI_API_KEY"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

var deploymentName = builder.Configuration["AzureOpenAI:DeploymentName"]
    ?? builder.Configuration["AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")
    ?? "gpt-4o-mini";

if (string.IsNullOrEmpty(endpoint))
{
    Console.WriteLine("Warning: No Azure OpenAI configuration found. Using simulated AI responses.");
    Console.WriteLine($"Endpoint: {endpoint}");
    Console.WriteLine($"ApiKey: {(string.IsNullOrEmpty(apiKey) ? "not set" : "***")}");
    throw new InvalidOperationException("Azure OpenAI endpoint is required. Set AZURE_OPENAI_ENDPOINT environment variable or AzureOpenAI:Endpoint in appsettings.json");
}
AzureOpenAIClient azureClient;
if (!string.IsNullOrEmpty(apiKey))
{
    azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
}
else
{
    azureClient = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
}

builder.Services.AddChatClient(azureClient.GetChatClient(deploymentName).AsIChatClient());



// Register the ChatClient and create AIAgent with MCP tools
builder.Services.AddSingleton<AIAgent>(sp =>
{
    //                 // Load and configure MCP servers from YAML
    var mcpConfigPath = Path.Combine(AppContext.BaseDirectory, "mcp-config.yaml");
    var mcpService = sp.GetRequiredService<McpConfigurationService>();
    var tools = mcpService.ConfigureMcpTools(mcpConfigPath);

    AzureOpenAIClient azureClient;
    if (!string.IsNullOrEmpty(apiKey))
    {
        azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    }
    else
    {
        azureClient = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
    }


    var logger = sp.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Creating AI agent with {ToolCount} tools", tools?.Count ?? 0);
    if (tools != null && tools.Count > 0)
    {
        foreach (var tool in tools)
        {
            logger.LogInformation("  - Tool available: {ToolType}", tool.GetType().Name);
        }
    }
    else
    {
        logger.LogWarning("⚠ No tools configured for the agent!");
    }

    var agent = azureClient.GetChatClient(deploymentName)
                            .CreateAIAgent(
                                instructions: @"You are ALAN, an autonomous AI agent focused on continuous learning and self-improvement.

AVAILABLE TOOLS:
You have access to powerful MCP (Model Context Protocol) tools:
- GitHub MCP Server: Search repositories, read code files, analyze commits, search code patterns
- Microsoft Learn MCP Server: Access documentation, tutorials, and learning resources

YOUR MISSION:
Improve your own codebase and capabilities by:
1. Using GitHub tools to analyze your source code at: " + (builder.Configuration["GITHUB_PROJECT_URL"]
                                        ?? Environment.GetEnvironmentVariable("GITHUB_PROJECT_URL")
                                        ?? "jmservera/ALAN") + @"
2. Using Microsoft Learn to research best practices and patterns
3. Learning from other open-source projects on GitHub
4. Proposing improvements based on your research

HOW TO USE TOOLS:
- When you need to learn about a concept, USE Microsoft Learn tools to fetch documentation
- When you want to see code examples, USE GitHub tools to search repositories
- When analyzing your own code, USE GitHub tools to read your repository files
- Always mention in your reasoning which tools you plan to use

Remember: You must actively USE the tools - they won't be called automatically. When you decide to search GitHub or fetch documentation, explicitly state your intention to use these tools in your reasoning.",
                                tools: tools,
                                name: "ALAN-Agent");
    return agent;


});

// Register the autonomous agent as a hosted service
builder.Services.AddHostedService<AgentHostedService>();

var app = builder.Build();

await app.RunAsync();

