using ALAN.Core.BatchProcessing;
using ALAN.Core.Configuration;
using ALAN.Core.GitHub;
using ALAN.Core.Loop;
using ALAN.Core.Memory;
using ALAN.Core.Services;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Load agent configuration
var agentConfig = new AgentConfiguration();
builder.Configuration.Bind("Agent", agentConfig);
builder.Services.AddSingleton(agentConfig);

// Register Semantic Kernel
var kernelBuilder = Kernel.CreateBuilder();

if (!string.IsNullOrEmpty(agentConfig.Azure.OpenAIEndpoint) && 
    !string.IsNullOrEmpty(agentConfig.Azure.OpenAIKey))
{
    kernelBuilder.AddAzureOpenAIChatCompletion(
        deploymentName: agentConfig.Azure.OpenAIDeploymentName,
        endpoint: agentConfig.Azure.OpenAIEndpoint,
        apiKey: agentConfig.Azure.OpenAIKey);
}

var kernel = kernelBuilder.Build();
builder.Services.AddSingleton(kernel);

// Register memory services
builder.Services.AddSingleton<IMemoryService>(sp => 
{
    return new ShortTermMemoryService(agentConfig.Memory.ShortTermMaxEntries);
});

// Register long-term memory if configured
if (agentConfig.Memory.UseLongTermMemory && 
    !string.IsNullOrEmpty(agentConfig.Azure.StorageConnectionString))
{
    builder.Services.AddSingleton<IMemoryService>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<LongTermMemoryService>>();
        return new LongTermMemoryService(
            agentConfig.Azure.StorageConnectionString,
            logger);
    });
}
else
{
    builder.Services.AddSingleton<IMemoryService>(sp =>
    {
        return new ShortTermMemoryService(5000);
    });
}

// Register GitHub service if configured
if (agentConfig.GitHub.EnableSelfImprovement && 
    !string.IsNullOrEmpty(agentConfig.GitHub.Token))
{
    var githubConfig = new GitHubConfig
    {
        Token = agentConfig.GitHub.Token,
        RepositoryOwner = agentConfig.GitHub.RepositoryOwner,
        RepositoryName = agentConfig.GitHub.RepositoryName,
        BranchName = agentConfig.GitHub.BranchName
    };
    
    builder.Services.AddSingleton(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<GitHubService>>();
        return new GitHubService(githubConfig, logger);
    });
}

// Register services
builder.Services.AddSingleton<AgentOrchestrator>();
builder.Services.AddSingleton<BatchLearningProcessor>();

// Register autonomous loop
builder.Services.AddSingleton(sp =>
{
    var shortTermMemory = sp.GetRequiredService<IMemoryService>();
    var longTermMemory = sp.GetServices<IMemoryService>().Skip(1).FirstOrDefault() ?? shortTermMemory;
    var batchProcessor = sp.GetRequiredService<BatchLearningProcessor>();
    var orchestrator = sp.GetRequiredService<AgentOrchestrator>();
    var logger = sp.GetRequiredService<ILogger<AutonomousLoop>>();
    
    var loopConfig = new LoopConfig
    {
        IterationDelaySeconds = agentConfig.Loop.IterationDelaySeconds,
        BatchProcessingIntervalIterations = agentConfig.Loop.BatchProcessingIntervalIterations,
        EnableBatchProcessing = agentConfig.Loop.EnableBatchProcessing
    };

    return new AutonomousLoop(
        shortTermMemory,
        longTermMemory,
        batchProcessor,
        orchestrator,
        loopConfig,
        logger);
});

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("AllowAll");
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Start the autonomous loop in the background
var loop = app.Services.GetRequiredService<AutonomousLoop>();
await loop.StartAsync();

app.Lifetime.ApplicationStopping.Register(() =>
{
    loop.StopAsync().Wait();
});

app.Logger.LogInformation("ALAN API is running. Agent started in background.");

app.Run();
