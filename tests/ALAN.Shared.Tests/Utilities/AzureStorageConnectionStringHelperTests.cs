using ALAN.Shared.Utilities;
using Xunit;

namespace ALAN.Shared.Tests.Utilities;

public class AzureStorageConnectionStringHelperTests
{
    [Fact]
    public void ExtractAccountName_WithAccountKey_ReturnsAccountName()
    {
        // Arrange
        var connectionString = "DefaultEndpointsProtocol=https;AccountName=mystorageaccount;AccountKey=mykey==;EndpointSuffix=core.windows.net";

        // Act
        var result = AzureStorageConnectionStringHelper.ExtractAccountName(connectionString);

        // Assert
        Assert.Equal("mystorageaccount", result);
    }

    [Fact]
    public void ExtractAccountName_WithManagedIdentityFormat_ReturnsAccountName()
    {
        // Arrange
        var connectionString = "DefaultEndpointsProtocol=https;AccountName=mystorageaccount;EndpointSuffix=core.windows.net";

        // Act
        var result = AzureStorageConnectionStringHelper.ExtractAccountName(connectionString);

        // Assert
        Assert.Equal("mystorageaccount", result);
    }

    [Fact]
    public void ExtractAccountName_WithDevelopmentStorage_ReturnsDevstoreaccount1()
    {
        // Arrange
        var connectionString = "UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://127.0.0.1:10000;AccountName=devstoreaccount1";

        // Act
        var result = AzureStorageConnectionStringHelper.ExtractAccountName(connectionString);

        // Assert
        Assert.Equal("devstoreaccount1", result);
    }

    [Fact]
    public void ExtractAccountName_WithoutAccountName_ThrowsArgumentException()
    {
        // Arrange
        var connectionString = "DefaultEndpointsProtocol=https;EndpointSuffix=core.windows.net";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            AzureStorageConnectionStringHelper.ExtractAccountName(connectionString));
        
        Assert.Contains("AccountName", exception.Message);
    }

    [Fact]
    public void ExtractAccountName_WithEmptyString_ThrowsArgumentException()
    {
        // Arrange
        var connectionString = "";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            AzureStorageConnectionStringHelper.ExtractAccountName(connectionString));
        
        Assert.Contains("AccountName", exception.Message);
    }

    [Fact]
    public void ExtractAccountName_WithAccountNameInDifferentCase_ReturnsAccountName()
    {
        // Arrange
        var connectionString = "DefaultEndpointsProtocol=https;accountname=mystorageaccount;EndpointSuffix=core.windows.net";

        // Act
        var result = AzureStorageConnectionStringHelper.ExtractAccountName(connectionString);

        // Assert
        Assert.Equal("mystorageaccount", result);
    }

    [Fact]
    public void ExtractAccountName_WithWhitespace_TrimsAccountName()
    {
        // Arrange
        var connectionString = "DefaultEndpointsProtocol=https;AccountName= mystorageaccount ;EndpointSuffix=core.windows.net";

        // Act
        var result = AzureStorageConnectionStringHelper.ExtractAccountName(connectionString);

        // Assert
        Assert.Equal("mystorageaccount", result);
    }
}
