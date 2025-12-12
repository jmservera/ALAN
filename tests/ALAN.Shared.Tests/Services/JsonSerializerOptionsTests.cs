using ALAN.Shared.Services.Memory;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace ALAN.Shared.Tests.Services;

public class JsonSerializerOptionsTests
{
    [Fact]
    public void AzureBlobShortTermMemoryService_JsonOptions_IsCaseInsensitive()
    {
        var options = typeof(AzureBlobShortTermMemoryService)
            .GetField("JsonOptions", BindingFlags.NonPublic | BindingFlags.Static)?
            .GetValue(null) as JsonSerializerOptions;

        Assert.NotNull(options);
        Assert.True(options!.PropertyNameCaseInsensitive);
    }

    [Fact]
    public void AzureBlobLongTermMemoryService_JsonOptions_IsCaseInsensitive()
    {
        var options = typeof(AzureBlobLongTermMemoryService)
            .GetField("JsonOptions", BindingFlags.NonPublic | BindingFlags.Static)?
            .GetValue(null) as JsonSerializerOptions;

        Assert.NotNull(options);
        Assert.True(options!.PropertyNameCaseInsensitive);
    }
}
