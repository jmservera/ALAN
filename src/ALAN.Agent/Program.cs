using ALAN.Agent.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

var builder = Host.CreateApplicationBuilder(args);

// Add configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Register services
builder.Services.AddSingleton<StateManager>();

// Configure Semantic Kernel
var kernelBuilder = builder.Services.AddKernel();

// Try to get API key from configuration
var apiKey = builder.Configuration["OpenAI:ApiKey"] 
    ?? builder.Configuration["OPENAI_API_KEY"]
    ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

var modelId = builder.Configuration["OpenAI:ModelId"] ?? "gpt-4o-mini";

if (!string.IsNullOrEmpty(apiKey))
{
    kernelBuilder.AddOpenAIChatCompletion(
        modelId: modelId,
        apiKey: apiKey);
}
else
{
    // Use a simulated service for demo purposes
    Console.WriteLine("Warning: No OpenAI API key found. Using simulated AI responses.");
}

// Register the autonomous agent as a hosted service
builder.Services.AddHostedService<AgentHostedService>();

var app = builder.Build();

await app.RunAsync();
