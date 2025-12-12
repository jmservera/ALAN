using ALAN.ChatApi.Controllers;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace ALAN.ChatApi.Tests.Controllers;

public class ChatWebSocketControllerTests
{
    [Fact]
    public void JsonOptions_IsCaseInsensitive()
    {
        var options = typeof(ChatWebSocketController)
            .GetField("JsonOptions", BindingFlags.NonPublic | BindingFlags.Static)?
            .GetValue(null) as JsonSerializerOptions;

        Assert.NotNull(options);
        Assert.True(options!.PropertyNameCaseInsensitive);
    }

    [Fact]
    public void JsonOptions_DeserializesRegardlessOfPropertyCasing()
    {
        var options = typeof(ChatWebSocketController)
            .GetField("JsonOptions", BindingFlags.NonPublic | BindingFlags.Static)?
            .GetValue(null) as JsonSerializerOptions;

        Assert.NotNull(options);

        var jsonUpper = @"{ ""ACTION"": ""chat"", ""MESSAGE"": ""Hi"" }";
        var msg = JsonSerializer.Deserialize<ChatWebSocketMessage>(jsonUpper, options!);

        Assert.NotNull(msg);
        Assert.Equal("chat", msg!.Action);
        Assert.Equal("Hi", msg.Message);
    }
}
