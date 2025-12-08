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
    if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(apiKey))
    {
        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        return azureClient
                .GetChatClient(deploymentName)
                .CreateAIAgent(instructions: "You are an autonomous AI agent. Think about interesting things and take actions to learn and explore.",
                            name: "ALAN Agent");

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
