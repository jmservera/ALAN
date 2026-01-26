# MCP Integration

ALAN integrates with Model Context Protocol (MCP) servers to access external tools and services through a YAML-based configuration system.

## Configuration

MCP servers are configured in `mcp-config.yaml`:

```yaml
mcp:
  servers:
    github:
      command: npx
      args:
        - -y
        - "@modelcontextprotocol/server-github"
      env:
        GITHUB_PERSONAL_ACCESS_TOKEN: ${GITHUB_PERSONAL_ACCESS_TOKEN}
    
    microsoft-learn:
      command: npx
      args:
        - -y
        - "@modelcontextprotocol/server-fetch"
      env:
        ALLOWED_DOMAINS: "learn.microsoft.com,docs.microsoft.com"
```

## How It Works

1. **YAML Configuration**: The `mcp-config.yaml` file defines MCP servers with their connection details
2. **Service Loading**: The `McpConfigurationService` loads the configuration at startup
3. **Agent Integration**: When Agent Framework's MCP support is available, servers are connected and tools are registered with the AI agent
4. **Tool Discovery**: MCP servers advertise available tools that the agent can use

## Environment Variables

MCP server configurations can reference environment variables using `${VARIABLE_NAME}` syntax:

```yaml
env:
  GITHUB_PERSONAL_ACCESS_TOKEN: ${GITHUB_PERSONAL_ACCESS_TOKEN}
```

Set the variable before starting the agent:

```bash
export GITHUB_PERSONAL_ACCESS_TOKEN="your-token-here"
```

## Available MCP Servers

### GitHub MCP Server
Package: `@modelcontextprotocol/server-github`

**Tools:**
- Repository operations (list, read files)
- Commit history
- Pull request management
- Code search
- Issue management

**Configuration:**
```yaml
github:
  command: npx
  args:
    - -y
    - "@modelcontextprotocol/server-github"
  env:
    GITHUB_PERSONAL_ACCESS_TOKEN: ${GITHUB_PERSONAL_ACCESS_TOKEN}
```

### Microsoft Learn MCP Server  
Package: `@modelcontextprotocol/server-fetch`

**Tools:**
- Fetch web content
- Documentation retrieval
- Learning path access

**Configuration:**
```yaml
microsoft-learn:
  command: npx
  args:
    - -y
    - "@modelcontextprotocol/server-fetch"
  env:
    ALLOWED_DOMAINS: "learn.microsoft.com,docs.microsoft.com"
```

## Adding New MCP Servers

To add a new MCP server:

1. Add server configuration to `mcp-config.yaml`:

```yaml
mcp:
  servers:
    my-server:
      command: path/to/executable
      args:
        - --option1
        - value1
      env:
        API_KEY: ${MY_API_KEY}
```

2. Set required environment variables
3. Restart the agent

The new server's tools will be automatically discovered and made available to the agent.

## Current Implementation Status

The MCP integration is fully functional with both transport types:

- ✅ Loads MCP configuration from YAML
- ✅ Parses server definitions (HTTP and stdio)
- ✅ Stdio transport support (npx, command-based servers)
- ✅ HTTP transport support (remote MCP servers)
- ✅ Environment variable resolution (`${VAR_NAME}` and `$VAR_NAME` syntax)
- ✅ Process lifecycle management (automatic cleanup on shutdown)
- ✅ Tool discovery and registration with AI agent
- ✅ Full integration with Semantic Kernel agent

### Supported Transports

#### Stdio Transport (Recommended for npx/local servers)
```yaml
github:
  command: npx
  args:
    - -y
    - "@modelcontextprotocol/server-github"
  env:
    GITHUB_PERSONAL_ACCESS_TOKEN: ${GITHUB_PERSONAL_ACCESS_TOKEN}
```

#### HTTP Transport (For remote servers)
```yaml
remote-server:
  type: http
  url: https://api.example.com/mcp
  pat: ${API_TOKEN}
```

## Usage in Agent

The agent automatically has access to all configured MCP tools:

```csharp
// The agent can naturally use MCP tools in its reasoning
// Example agent thought: "I need to read a file from GitHub..."
// The agent will automatically invoke the GitHub MCP tool
```

## Security Considerations

### API Keys and Tokens
- Store sensitive values in environment variables
- Never commit tokens to source control
- Use Azure Key Vault for production deployments
- Rotate tokens regularly
- Sensitive env vars (containing TOKEN, KEY, SECRET) are masked in logs

### Domain Restrictions
For fetch-based servers, restrict allowed domains:

```yaml
env:
  ALLOWED_DOMAINS: "trusted-domain.com,another-domain.com"
```

### Command Execution
- MCP servers execute as separate processes managed by `McpConfigurationService`
- Processes are automatically terminated when the agent shuts down
- Validate server sources before adding to configuration
- Monitor resource usage of MCP server processes
- stderr output from MCP processes is logged for debugging

## Troubleshooting

### MCP Configuration Not Loading
- Verify `mcp-config.yaml` exists in the application directory
- Check YAML syntax validity
- Review logs for parsing errors

### Environment Variables Not Resolved
- Ensure variables are set before starting the agent
- Use `${VAR_NAME}` syntax in YAML
- Check variable names match exactly (case-sensitive)

### Server Connection Issues
- Verify server command is accessible (e.g., `npx` is in PATH)
- Check network connectivity for remote servers
- Review server-specific logs

## Examples

### Full Configuration Example

```yaml
mcp:
  servers:
    github:
      command: npx
      args:
        - -y
        - "@modelcontextprotocol/server-github"
      env:
        GITHUB_PERSONAL_ACCESS_TOKEN: ${GITHUB_PERSONAL_ACCESS_TOKEN}
    
    filesystem:
      command: npx
      args:
        - -y
        - "@modelcontextprotocol/server-filesystem"
      env:
        ALLOWED_DIRECTORIES: "/path/to/allowed,/another/path"
    
    custom-api:
      command: python
      args:
        - /path/to/custom-mcp-server.py
      env:
        API_ENDPOINT: ${CUSTOM_API_ENDPOINT}
        API_KEY: ${CUSTOM_API_KEY}
```

### Starting with Environment Variables

```bash
# Set required environment variables
export GITHUB_PERSONAL_ACCESS_TOKEN="ghp_xxx"
export CUSTOM_API_ENDPOINT="https://api.example.com"
export CUSTOM_API_KEY="xxx"

# Start the agent
cd src/ALAN.Agent
dotnet run
```

## Future Enhancements

- Hot-reload of MCP configuration without restart
- MCP server health monitoring
- Tool usage analytics and logging
- Automatic retry for failed tool invocations
- Support for MCP server authentication methods
- Integration with Azure-hosted MCP servers
