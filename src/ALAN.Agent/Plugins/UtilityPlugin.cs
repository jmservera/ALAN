using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace ALAN.Agent.Plugins;

/// <summary>
/// Plugin providing utility functions for the agent.
/// </summary>
public static class UtilityPlugin
{
    /// <summary>
    /// Gets the current date and time in UTC.
    /// </summary>
    [Description("Gets the current date and time in UTC format")]
    public static string GetCurrentDateTime()
    {
        return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
    }

    /// <summary>
    /// Gets the current date and time in a specific format.
    /// </summary>
    [Description("Gets the current date and time in a specified format")]
    public static string GetFormattedDateTime(
        [Description("The format string (e.g., 'yyyy-MM-dd', 'HH:mm:ss')")] string format)
    {
        try
        {
            return DateTime.UtcNow.ToString(format);
        }
        catch (FormatException)
        {
            return $"Invalid format: {format}. Current time: {DateTime.UtcNow}";
        }
    }

    /// <summary>
    /// Gets the current Unix timestamp.
    /// </summary>
    [Description("Gets the current Unix timestamp (seconds since epoch)")]
    public static long GetUnixTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    /// <summary>
    /// Calculates time elapsed since a given timestamp.
    /// </summary>
    [Description("Calculates time elapsed since a given ISO 8601 timestamp")]
    public static string GetTimeSince(
        [Description("The starting timestamp in ISO 8601 format")] string timestamp)
    {
        try
        {
            var startTime = DateTime.Parse(timestamp);
            var elapsed = DateTime.UtcNow - startTime;
            
            if (elapsed.TotalDays >= 1)
                return $"{elapsed.TotalDays:F1} days";
            if (elapsed.TotalHours >= 1)
                return $"{elapsed.TotalHours:F1} hours";
            if (elapsed.TotalMinutes >= 1)
                return $"{elapsed.TotalMinutes:F1} minutes";
            return $"{elapsed.TotalSeconds:F1} seconds";
        }
        catch (FormatException)
        {
            return $"Invalid timestamp format: {timestamp}";
        }
    }

    /// <summary>
    /// Generates a new GUID.
    /// </summary>
    [Description("Generates a new globally unique identifier (GUID)")]
    public static string GenerateGuid()
    {
        return Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Formats a time duration.
    /// </summary>
    [Description("Formats a duration in seconds into a human-readable string")]
    public static string FormatDuration(
        [Description("Duration in seconds")] double seconds)
    {
        var timeSpan = TimeSpan.FromSeconds(seconds);
        
        if (timeSpan.TotalDays >= 1)
            return $"{timeSpan.TotalDays:F1} days";
        if (timeSpan.TotalHours >= 1)
            return $"{timeSpan.TotalHours:F1} hours";
        if (timeSpan.TotalMinutes >= 1)
            return $"{timeSpan.TotalMinutes:F1} minutes";
        return $"{timeSpan.TotalSeconds:F1} seconds";
    }

    public static IEnumerable<AITool> AsTools()
    {
        return
        [
            AIFunctionFactory.Create(GetCurrentDateTime),
            AIFunctionFactory.Create(GetFormattedDateTime),
            AIFunctionFactory.Create(GetUnixTimestamp),
            AIFunctionFactory.Create(GetTimeSince),
            AIFunctionFactory.Create(GenerateGuid),
            AIFunctionFactory.Create(FormatDuration)
        ];

    }
}
