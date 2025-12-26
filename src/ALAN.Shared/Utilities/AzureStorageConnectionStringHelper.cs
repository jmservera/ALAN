namespace ALAN.Shared.Utilities;

/// <summary>
/// Helper class for parsing Azure Storage connection strings
/// </summary>
public static class AzureStorageConnectionStringHelper
{
    /// <summary>
    /// Extracts the AccountName from an Azure Storage connection string
    /// </summary>
    /// <param name="connectionString">The connection string to parse</param>
    /// <returns>The account name</returns>
    /// <exception cref="ArgumentException">Thrown when connection string doesn't contain AccountName</exception>
    public static string ExtractAccountName(string connectionString)
    {
        // Parse connection string to extract AccountName
        var accountName = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(keyValue => keyValue.Length == 2 && keyValue[0].Trim().Equals("AccountName", StringComparison.OrdinalIgnoreCase))
            .Select(keyValue => keyValue[1].Trim())
            .FirstOrDefault();

        if (string.IsNullOrEmpty(accountName))
        {
            throw new ArgumentException("Connection string must contain AccountName for managed identity authentication");
        }

        return accountName;
    }
}
