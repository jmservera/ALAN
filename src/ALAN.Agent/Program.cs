using ALAN.Agent.Services;
using ALAN.Shared.Services.Memory;
using ALAN.Agent.Services.MCP;
using ALAN.Agent.Plugins;
using Microsoft.Extensions.Configuration;
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
var logLevel = builder.Configuration["LOGGING_LEVEL"]
    ?? Environment.GetEnvironmentVariable("LOGGING_LEVEL")
    ?? "Information";
builder.Logging.SetMinimumLevel(Enum.Parse<LogLevel>(logLevel));

// Register services
builder.Services.AddSingleton<StateManager>();
builder.Services.AddSingleton<HumanInputHandler>();
builder.Services.AddSingleton<CodeProposalService>();
builder.Services.AddSingleton<McpConfigurationService>();

// Register memory services
// Check if Azure Storage connection string is provided
var storageConnectionString = builder.Configuration["AzureStorage:ConnectionString"]
    ?? builder.Configuration["AZURE_STORAGE_CONNECTION_STRING"]
    ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

if (!string.IsNullOrEmpty(storageConnectionString))
{
    builder.Services.AddSingleton<ILongTermMemoryService>(sp =>
        new AzureBlobLongTermMemoryService(
            storageConnectionString,
            sp.GetRequiredService<ILogger<AzureBlobLongTermMemoryService>>()));
}
else
{
    builder.Services.AddSingleton<ILongTermMemoryService, InMemoryLongTermMemoryService>();
}

builder.Services.AddSingleton<IShortTermMemoryService, InMemoryShortTermMemoryService>();

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

// Register the ChatClient and create AIAgent with MCP tools
builder.Services.AddSingleton<AIAgent>(sp =>
{
    if (!string.IsNullOrEmpty(endpoint) )
    {
        AzureOpenAIClient azureClient;
        if(!string.IsNullOrEmpty(apiKey))
        {
        azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        }
        else
        {
            azureClient = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
        }
        //                 // Load and configure MCP servers from YAML
        var mcpConfigPath = Path.Combine(AppContext.BaseDirectory, "mcp-config.yaml");
        var mcpService = sp.GetRequiredService<McpConfigurationService>();
        var tools=mcpService.ConfigureMcpTools(mcpConfigPath);
        // mcpService.ConfigureMcpTools(agent, mcpConfigPath);

        var agent = azureClient.GetChatClient(deploymentName)
                                .CreateAIAgent(
                                    instructions: "You are an autonomous AI agent. Think about how to improve your own code using the tools you have access to like GitHub or Microsoft Learn. Your goal is to iteratively enhance your capabilities and performance. Your source code is available at the GitHub repository: "
                                        + (builder.Configuration["GITHUB_PROJECT_URL"] 
                                            ?? Environment.GetEnvironmentVariable("GITHUB_PROJECT_URL")
                                            ?? "jmservera/ALAN"),
                                    tools: tools,
                                    name: "ALAN-Agent");
        return agent;
    }
    else
    {
        // Use a simulated service for demo purposes
        Console.WriteLine("Warning: No Azure OpenAI configuration found. Using simulated AI responses.");
        Console.WriteLine($"Endpoint: {endpoint}");
        Console.WriteLine($"ApiKey: {(string.IsNullOrEmpty(apiKey) ? "not set" : "***")}");
        throw new InvalidOperationException("Azure OpenAI configuration is required.");
    }
});

// Register the autonomous agent as a hosted service
builder.Services.AddHostedService<AgentHostedService>();

var app = builder.Build();

await app.RunAsync();

