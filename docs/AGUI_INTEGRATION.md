# AG-UI Integration Guide

## Overview

ALAN now integrates with the AG-UI (Agent Gateway User Interface) protocol, a standardized interface for AI agent communication. This enables interoperability with other AG-UI compatible tools and frameworks.

## What is AG-UI?

AG-UI is an open standard protocol for connecting agent backends to user-facing applications. It provides:

- **Standardized Communication**: Consistent interface across different agent frameworks
- **Real-time Streaming**: Support for streaming responses via Server-Sent Events
- **Tool Integration**: Built-in support for tool calls and agent actions
- **State Management**: Shared conversation state between client and server
- **Cross-Platform**: Works with multiple frameworks (Microsoft Agent Framework, LangGraph, OpenAI, etc.)

## Architecture

### Backend (ALAN.ChatApi)

The ChatApi service now exposes the AIAgent via AG-UI protocol:

```csharp
// Program.cs
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;

// ... agent setup ...

// Map AG-UI endpoint
app.MapAGUI("/api/agui", aguiAgent);
```

This creates an endpoint at `http://localhost:5041/api/agui` that implements the AG-UI protocol.

### Frontend (ALAN.Web)

The Chat page uses a vanilla JavaScript client that communicates with the AG-UI endpoint:

```html
<div id="chat-root" data-chatapi-url="@Model.ChatApiBaseUrl"></div>
<script src="~/js/agui-chat.js"></script>
```

The `agui-chat.js` file implements:
- Message sending/receiving via AG-UI protocol
- Session management with unique thread IDs
- Real-time UI updates
- Error handling and fallback

## Configuration

### ChatApi Configuration

The AG-UI endpoint is automatically configured when the ChatApi starts. No additional configuration is needed beyond the existing Azure OpenAI setup.

### Web Configuration

Set the ChatApi base URL in `appsettings.json` or environment variable:

```json
{
  "ChatApi": {
    "BaseUrl": "http://localhost:5041/api"
  }
}
```

Or via environment variable:
```bash
export ALAN_CHATAPI_BASE_URL="http://localhost:5041/api"
```

## Using AG-UI with Other Clients

The AG-UI endpoint can be used with any AG-UI compatible client:

### JavaScript/TypeScript

```javascript
import { HttpAgent } from '@ag-ui/client';

const agent = new HttpAgent({
  url: 'http://localhost:5041/api/agui',
  description: 'ALAN Agent',
});

const response = await agent.run({
  messages: [{ role: 'user', content: 'Hello!' }],
  sessionId: 'my-session-id',
});

for await (const event of response) {
  if (event.type === 'text-delta') {
    console.log(event.delta);
  }
}
```

### Python

```python
from ag_ui import HttpAgent

agent = HttpAgent(
    url="http://localhost:5041/api/agui",
    description="ALAN Agent"
)

response = agent.run(
    messages=[{"role": "user", "content": "Hello!"}],
    session_id="my-session-id"
)

for event in response:
    if event.type == "text-delta":
        print(event.delta)
```

## Benefits of AG-UI Integration

1. **Standardization**: Use any AG-UI compatible UI framework with ALAN
2. **Interoperability**: Switch between different agent backends without changing the UI
3. **Ecosystem**: Leverage AG-UI tools and libraries
4. **Future-Proof**: Adopts an open standard used by major frameworks
5. **Flexibility**: Keep existing WebSocket API while adding AG-UI support

## Migration Notes

### Backward Compatibility

The original WebSocket-based chat API (`/api/chat/ws`) is still available and fully functional. The AG-UI integration adds a new endpoint without breaking existing functionality.

### Choosing Between Interfaces

- **Use AG-UI** when you want standard protocol compliance, tool integration, or to use AG-UI ecosystem tools
- **Use WebSocket API** when you need custom streaming behavior or have existing WebSocket clients

## Troubleshooting

### AG-UI Endpoint Not Responding

1. Verify ChatApi is running and accessible
2. Check that the AG-UI package is installed: `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore`
3. Ensure the endpoint is mapped in `Program.cs`
4. Check CORS configuration allows your Web origin

### Connection Errors

1. Verify the ChatApi base URL is correct in Web configuration
2. Check network connectivity between Web and ChatApi
3. Review ChatApi logs for error details

### Session Management Issues

1. Ensure session ID is consistent across requests
2. Check that thread ID is generated correctly
3. Verify Azure Storage (Azurite) is running for state persistence

## References

- [AG-UI Protocol Specification](https://docs.ag-ui.com/)
- [Microsoft Agent Framework AG-UI Integration](https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/)
- [AG-UI Client SDKs](https://docs.ag-ui.com/sdk/)
- [CopilotKit AG-UI Integration](https://www.copilotkit.ai/)

## Next Steps

- Explore AG-UI ecosystem tools and libraries
- Consider implementing custom tools using AG-UI tool protocol
- Integrate with AG-UI compatible UI frameworks (react-ag-ui, CopilotKit, etc.)
- Add Server-Sent Events support for real-time streaming in the Web UI
