using ALAN.Agent.Services;
using ALAN.Agent.Services.MCP;
using ALAN.Shared.Services;
using ALAN.Shared.Services.Memory;
using ALAN.Shared.Services.Queue;
using ALAN.Shared.Models;
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
builder.Services.AddSingleton<IPromptService, PromptService>();

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

// Register queue service for steering commands (human inputs)
// Chat is handled by the separate ChatApi service via WebSockets
builder.Services.AddSingleton<IMessageQueue<HumanInput>>(sp =>
    new AzureStorageQueueService<HumanInput>(
        storageConnectionString,
        "human-inputs",
        sp.GetRequiredService<ILogger<AzureStorageQueueService<HumanInput>>>()));

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
azureClient = !string.IsNullOrEmpty(apiKey)
    ? new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
    : new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());

builder.Services.AddChatClient(azureClient.GetChatClient(deploymentName).AsIChatClient());



// Register the ChatClient and create AIAgent with MCP tools
builder.Services.AddSingleton<AIAgent>(sp =>
{
    //                 // Load and configure MCP servers from YAML
    var mcpConfigPath = Path.Combine(AppContext.BaseDirectory, "mcp-config.yaml");
    var mcpService = sp.GetRequiredService<McpConfigurationService>();
    var tools = mcpService.ConfigureMcpTools(mcpConfigPath).GetAwaiter().GetResult();


    AzureOpenAIClient azureClient;
    AzureOpenAIClientOptions azureOptions = new AzureOpenAIClientOptions(
        AzureOpenAIClientOptions.ServiceVersion.V2024_12_01_Preview
    );
    if (!string.IsNullOrEmpty(apiKey))
    {
        azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey), azureOptions);
    }
    else
    {
        azureClient = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential(), azureOptions);
    }


    var logger = sp.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Creating AI agent with {ToolCount} tools", tools?.Count ?? 0);
    if (tools != null && tools.Count > 0)
    {
        foreach (var tool in tools)
        {
            logger.LogInformation("  - Tool available: {ToolType} {ToolName}", tool.GetType().Name, tool.Name);
        }
    }
    else
    {
        logger.LogWarning("⚠ No tools configured for the agent!");
    }

    // Get project URL for agent instructions
    var projectUrl = builder.Configuration["GITHUB_PROJECT_URL"]
                        ?? Environment.GetEnvironmentVariable("GITHUB_PROJECT_URL")
                        ?? "jmservera/ALAN";

    // Render agent instructions from template
    var promptService = sp.GetRequiredService<IPromptService>();
    var instructions = promptService.RenderTemplate("agent-instructions", new { projectUrl });

    var agent = azureClient.GetChatClient(deploymentName)
                            .CreateAIAgent(
                                instructions: instructions,
                                tools: tools,
                                name: "ALAN-Agent");
    return agent;


});

// Register the autonomous agent as a hosted service
builder.Services.AddHostedService<AgentHostedService>();

var app = builder.Build();

await app.RunAsync();

