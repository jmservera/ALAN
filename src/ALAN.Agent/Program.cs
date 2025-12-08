using ALAN.Agent.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

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

// Configure Semantic Kernel
var kernelBuilder = builder.Services.AddKernel();

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

if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(apiKey))
{
    kernelBuilder.AddAzureOpenAIChatCompletion(
        deploymentName: deploymentName,
        endpoint: endpoint,
        apiKey: apiKey);
}
else
{
    // Use a simulated service for demo purposes
    Console.WriteLine("Warning: No Azure OpenAI configuration found. Using simulated AI responses.");
    Console.WriteLine($"Endpoint: {endpoint}");
    Console.WriteLine($"ApiKey: {(string.IsNullOrEmpty(apiKey) ? "not set" : "***")}");
}

// Register the autonomous agent as a hosted service
builder.Services.AddHostedService<AgentHostedService>();

var app = builder.Build();

await app.RunAsync();
