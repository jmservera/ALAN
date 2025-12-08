using ALAN.Core.BatchProcessing;
using ALAN.Core.Configuration;
using ALAN.Core.GitHub;
using ALAN.Core.Loop;
using ALAN.Core.Memory;
using ALAN.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace ALAN.Agent;

/// <summary>
/// Main entry point for the ALAN (Autonomous Learning Agent Network) agent
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== ALAN - Autonomous Learning Agent Network ===");
        Console.WriteLine("Initializing agent...\n");

        var host = CreateHostBuilder(args).Build();

        // Get the autonomous loop and start it
        var loop = host.Services.GetRequiredService<AutonomousLoop>();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        // Handle graceful shutdown
        var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
            logger.LogInformation("Shutdown requested...");
        };

        try
        {
            // Start the autonomous loop
            await loop.StartAsync();
            logger.LogInformation("Autonomous agent is now running. Press Ctrl+C to stop.");

            // Wait for cancellation
            await Task.Delay(Timeout.Infinite, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Shutting down gracefully...");
        }
        finally
        {
            await loop.StopAsync();
            logger.LogInformation("Agent stopped. Goodbye!");
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                      .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true)
                      .AddEnvironmentVariables()
                      .AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                // Load configuration
                var agentConfig = new AgentConfiguration();
                context.Configuration.Bind("Agent", agentConfig);

                // Register configuration
                services.AddSingleton(agentConfig);

                // Register Semantic Kernel
                var kernelBuilder = Kernel.CreateBuilder();
                
                // Configure Azure OpenAI if credentials are provided
                if (!string.IsNullOrEmpty(agentConfig.Azure.OpenAIEndpoint) && 
                    !string.IsNullOrEmpty(agentConfig.Azure.OpenAIKey))
                {
                    kernelBuilder.AddAzureOpenAIChatCompletion(
                        deploymentName: agentConfig.Azure.OpenAIDeploymentName,
                        endpoint: agentConfig.Azure.OpenAIEndpoint,
                        apiKey: agentConfig.Azure.OpenAIKey);
                }

                var kernel = kernelBuilder.Build();
                services.AddSingleton(kernel);

                // Register memory services
                services.AddSingleton<IMemoryService>(sp => 
                {
                    var logger = sp.GetRequiredService<ILogger<ShortTermMemoryService>>();
                    return new ShortTermMemoryService(agentConfig.Memory.ShortTermMaxEntries);
                });

                // Register long-term memory if configured
                if (agentConfig.Memory.UseLongTermMemory && 
                    !string.IsNullOrEmpty(agentConfig.Azure.StorageConnectionString))
                {
                    services.AddSingleton<IMemoryService>(sp =>
                    {
                        var logger = sp.GetRequiredService<ILogger<LongTermMemoryService>>();
                        return new LongTermMemoryService(
                            agentConfig.Azure.StorageConnectionString,
                            logger);
                    });
                }
                else
                {
                    // Use short-term memory as fallback for long-term
                    services.AddSingleton<IMemoryService>(sp =>
                    {
                        var logger = sp.GetRequiredService<ILogger<ShortTermMemoryService>>();
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
                    
                    services.AddSingleton(sp =>
                    {
                        var logger = sp.GetRequiredService<ILogger<GitHubService>>();
                        return new GitHubService(githubConfig, logger);
                    });
                }

                // Register services
                services.AddSingleton<AgentOrchestrator>();
                services.AddSingleton<BatchLearningProcessor>();

                // Register autonomous loop
                services.AddSingleton(sp =>
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
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            });
}
