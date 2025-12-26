using ALAN.ChatApi.Services;
using ALAN.Shared.Services;
using ALAN.Shared.Services.Memory;
using ALAN.Shared.Services.Queue;
using ALAN.Shared.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure;
using OpenAI;
using Microsoft.AspNetCore.HttpLogging;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = HttpLoggingFields.RequestPropertiesAndHeaders | HttpLoggingFields.RequestBody
        | HttpLoggingFields.ResponsePropertiesAndHeaders | HttpLoggingFields.ResponseBody;
    logging.RequestBodyLogLimit = int.MaxValue;
    logging.ResponseBodyLogLimit = int.MaxValue;
});
builder.Services.AddHttpClient().AddLogging();
builder.Services.AddAGUI();
// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
// Register PromptService
builder.Services.AddSingleton<IPromptService, PromptService>();

// Configure CORS for WebSocket connections
// Read allowed origins from configuration or environment variable, fallback to ALAN.Web defaults
var allowedOrigins = builder.Configuration["AllowedOrigins"]
    ?? Environment.GetEnvironmentVariable("ALAN_CHATAPI_ALLOWED_ORIGINS")
    ?? "http://localhost:5269,https://localhost:7049";
var allowedOriginsArray = allowedOrigins
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOriginsArray)
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

builder.Services.AddSingleton<IShortTermMemoryService>(sp =>
    new AzureBlobShortTermMemoryService(
        storageConnectionString,
        sp.GetRequiredService<ILogger<AzureBlobShortTermMemoryService>>()));

// Register queue service for steering commands (human inputs)
builder.Services.AddSingleton<IMessageQueue<HumanInput>>(sp =>
    new AzureStorageQueueService<HumanInput>(
        storageConnectionString,
        "human-inputs",
        sp.GetRequiredService<ILogger<AzureStorageQueueService<HumanInput>>>()));

// Register AgentStateService as both a singleton and hosted service
builder.Services.AddSingleton<AgentStateService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AgentStateService>());

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

    // Render agent instructions from template
    var promptService = sp.GetRequiredService<IPromptService>();
    var instructions = promptService.RenderTemplate("chat-agent-instructions", new { });

    var agentName = builder.Configuration["ChatApi:AgentName"]
        ?? Environment.GetEnvironmentVariable("ALAN_AGENT_NAME")
        ?? "alanagent";

    var agent = azureClient.GetChatClient(deploymentName)
                             .AsIChatClient()
                          .CreateAIAgent(
                              instructions: instructions,
                              name: agentName);
    return agent;
});

var app = builder.Build();
app.UseHttpLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseHttpsRedirection();
}

app.UseCors();

app.UseAuthorization();
app.MapControllers();

// Map AG-UI endpoint
// This exposes the AIAgent via the AG-UI protocol at /copilotkit endpoint
// The AG-UI protocol enables streaming chat, tool calls, and state management
var aguiAgent = app.Services.GetRequiredService<AIAgent>();
app.MapAGUI("/copilotkit", aguiAgent);

await app.RunAsync();
