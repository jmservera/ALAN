using ALAN.ChatApi.Services;
using ALAN.Shared.Services.Memory;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure CORS for WebSocket connections
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5274", "https://localhost:7274")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Register memory services - Azure Storage is required
var storageConnectionString = builder.Configuration["AzureStorage:ConnectionString"]
    ?? builder.Configuration["AZURE_STORAGE_CONNECTION_STRING"]
    ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

if (string.IsNullOrEmpty(storageConnectionString))
{
    throw new InvalidOperationException("Azure Storage connection string is required.");
}

builder.Services.AddSingleton<ILongTermMemoryService>(sp =>
    new AzureBlobLongTermMemoryService(
        storageConnectionString,
        sp.GetRequiredService<ILogger<AzureBlobLongTermMemoryService>>()));

// Configure Azure OpenAI
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
    throw new InvalidOperationException("Azure OpenAI endpoint is required.");
}

// Create AIAgent for chat
builder.Services.AddSingleton<AIAgent>(sp =>
{
    AzureOpenAIClient azureClient;

    if (!string.IsNullOrEmpty(apiKey))
    {
        azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    }
    else
    {
        azureClient = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
    }

    var agent = azureClient.GetChatClient(deploymentName)
                          .CreateAIAgent(
                              instructions: "You are ALAN, an autonomous AI agent focused on continuous learning and self-improvement. Respond naturally and conversationally to user questions.",
                              name: "ALAN-Chat");
    return agent;
});

// Register ChatService
builder.Services.AddSingleton<ChatService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseCors();

// Enable WebSocket support
var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
};
app.UseWebSockets(webSocketOptions);

app.UseAuthorization();
app.MapControllers();

app.Run();
