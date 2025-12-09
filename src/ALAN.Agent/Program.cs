using ALAN.Agent.Services;
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


// Register the ChatClient and create AIAgent
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
        return azureClient
                .GetChatClient(deploymentName)
                .CreateAIAgent(instructions: "You are an autonomous AI agent. Think about interesting things and take actions to learn and explore.",
                            name: "ALAN-Agent");
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
