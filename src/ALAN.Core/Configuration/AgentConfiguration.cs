using System;

namespace ALAN.Core.Configuration;

/// <summary>
/// Main configuration for the ALAN agent
/// </summary>
public class AgentConfiguration
{
    public AzureConfig Azure { get; set; } = new();
    public GitHubConfigSettings GitHub { get; set; } = new();
    public LoopConfigSettings Loop { get; set; } = new();
    public MemoryConfig Memory { get; set; } = new();
}

/// <summary>
/// Azure service configuration
/// </summary>
public class AzureConfig
{
    public string OpenAIEndpoint { get; set; } = string.Empty;
    public string OpenAIKey { get; set; } = string.Empty;
    public string OpenAIDeploymentName { get; set; } = "gpt-4";
    public string StorageConnectionString { get; set; } = string.Empty;
    public string SearchEndpoint { get; set; } = string.Empty;
    public string SearchKey { get; set; } = string.Empty;
}

/// <summary>
/// GitHub integration configuration
/// </summary>
public class GitHubConfigSettings
{
    public string Token { get; set; } = string.Empty;
    public string RepositoryOwner { get; set; } = string.Empty;
    public string RepositoryName { get; set; } = string.Empty;
    public string BranchName { get; set; } = "main";
    public bool EnableSelfImprovement { get; set; } = false;
}

/// <summary>
/// Loop configuration
/// </summary>
public class LoopConfigSettings
{
    public int IterationDelaySeconds { get; set; } = 30;
    public int BatchProcessingIntervalIterations { get; set; } = 100;
    public bool EnableBatchProcessing { get; set; } = true;
}

/// <summary>
/// Memory configuration
/// </summary>
public class MemoryConfig
{
    public int ShortTermMaxEntries { get; set; } = 1000;
    public bool UseLongTermMemory { get; set; } = true;
}
