# ALAN Agent Tools

This directory contains AI tools that extend the capabilities of the ALAN autonomous agent.

## GoogleSearchTool

The `GoogleSearchTool` provides web search capabilities using the Google Custom Search API. It allows the agent to search the internet for information, facts, and current knowledge.

### Features

- **Web Search**: Search Google for information using natural language queries
- **Configurable Results**: Control the number of results returned (1-10)
- **Structured Output**: Returns formatted search results with titles, URLs, and snippets
- **Error Handling**: Gracefully handles API errors and rate limits
- **Logging**: Comprehensive logging for debugging and monitoring

### Setup

#### 1. Get Google Custom Search API Credentials

1. **Create a Google Cloud Project**:

   - Go to [Google Cloud Console](https://console.cloud.google.com/)
   - Create a new project or select an existing one

2. **Enable the Custom Search API**:

   - Navigate to "APIs & Services" > "Library"
   - Search for "Custom Search API"
   - Click "Enable"

3. **Create API Credentials**:

   - Go to "APIs & Services" > "Credentials"
   - Click "Create Credentials" > "API Key"
   - Copy your API key

4. **Create a Programmable Search Engine**:
   - Go to [Programmable Search Engine](https://programmablesearchengine.google.com/)
   - Click "Add" to create a new search engine
   - Configure your search engine:
     - Sites to search: "Search the entire web"
     - Name: "ALAN Search Engine" (or your preference)
   - Click "Create"
   - Copy your Search Engine ID (cx parameter)

#### 2. Configure Environment Variables

Add your credentials to your environment or `appsettings.json`:

**Option A: Environment Variables** (Recommended for production)

```bash
export GOOGLE_SEARCH_API_KEY="your-api-key-here"
export GOOGLE_SEARCH_ENGINE_ID="your-search-engine-id-here"
```

**Option B: Configuration File** (For development)

```json
{
  "GoogleSearch": {
    "ApiKey": "your-api-key-here",
    "SearchEngineId": "your-search-engine-id-here"
  }
}
```

### Usage

The tool is automatically registered with the AI Agent when both `GOOGLE_SEARCH_API_KEY` and `GOOGLE_SEARCH_ENGINE_ID` are configured.

The agent can invoke the search tool using natural language. Examples:

- "Search the web for recent developments in quantum computing"
- "Find information about renewable energy trends"
- "What are the latest AI safety guidelines?"

### API Details

#### SearchWebAsync Method

```csharp
public async Task<string> SearchWebAsync(
    string query,           // The search query
    int maxResults = 5)     // Max results (1-10)
```

**Parameters:**

- `query`: The search query string
- `maxResults`: Maximum number of results to return (defaults to 5, clamped between 1-10)

**Returns:**
A formatted string containing:

- Search query
- Number of results found
- For each result:
  - Title
  - URL
  - Snippet/description

**Example Output:**

```
Search results for: quantum computing
Found 5 result(s)

Result 1:
Title: Introduction to Quantum Computing
URL: https://example.com/quantum-intro
Summary: Quantum computing is a revolutionary technology...

Result 2:
...
```

### Cost Considerations

Google Custom Search API has the following limits:

- **Free Tier**: 100 queries per day
- **Paid Tier**: $5 per 1,000 queries after free tier

For the ALAN agent with default settings (4,000 loops/day), consider:

- Not all loops will trigger a search
- The agent decides when to use the search tool based on need
- Monitor your usage in the [Google Cloud Console](https://console.cloud.google.com/)

### Integration with Agent Framework

The tool integrates seamlessly with Microsoft.Extensions.AI:

```csharp
// Tool is created and registered in Program.cs
var searchTool = new GoogleSearchTool(apiKey, searchEngineId, logger);
var searchFunction = GoogleSearchTool.CreateAIFunction(searchTool);
tools.Add(searchFunction);

// The agent automatically:
// 1. Decides when to use the search tool
// 2. Formulates appropriate queries
// 3. Processes the search results
// 4. Incorporates findings into its reasoning
```

### Troubleshooting

#### "Google Search API key or Search Engine ID not configured"

- Verify environment variables are set correctly
- Check `appsettings.json` configuration
- Ensure values are not empty strings

#### "Search error: The API key is invalid"

- Verify your API key is correct
- Check that Custom Search API is enabled in your Google Cloud project
- Ensure there are no extra spaces or quotes in the configuration

#### "Search error: Daily Limit Exceeded"

- You've exceeded the free tier limit (100 queries/day)
- Consider upgrading to a paid plan
- Reduce agent search frequency by adjusting prompts

#### Rate Limiting

The Google Custom Search API has rate limits:

- Monitor usage in Google Cloud Console
- Consider implementing caching for repeated queries
- The agent framework handles backoff automatically

### Example Agent Behavior

When the search tool is enabled, the agent can:

1. **Learn Current Information**:

   - "I wonder what the latest developments in AI are. Let me search..."
   - Invokes: `search_web("latest AI developments 2025")`

2. **Verify Facts**:

   - "I should verify this information about climate change..."
   - Invokes: `search_web("climate change current statistics")`

3. **Explore Topics**:
   - "I'm curious about Mars exploration. Let me find out more..."
   - Invokes: `search_web("Mars exploration recent missions")`

### Security Best Practices

1. **Never commit API keys** to version control
2. **Use environment variables** in production
3. **Rotate keys regularly** in Google Cloud Console
4. **Monitor usage** to detect unauthorized access
5. **Set up billing alerts** to prevent unexpected charges

### Future Enhancements

Potential improvements for the search tool:

- [ ] Implement result caching to reduce API calls
- [ ] Add image search capabilities
- [ ] Support advanced search operators
- [ ] Implement search result filtering
- [ ] Add news-specific search mode
- [ ] Support multiple search engines (fallback)
- [ ] Implement semantic ranking of results
- [ ] Add citation/source tracking for agent responses

### Additional Resources

- [Google Custom Search API Documentation](https://developers.google.com/custom-search/v1/overview)
- [Programmable Search Engine Help](https://support.google.com/programmable-search/)
- [API Pricing](https://developers.google.com/custom-search/v1/overview#pricing)
- [Microsoft.Extensions.AI Documentation](https://github.com/dotnet/extensions)
