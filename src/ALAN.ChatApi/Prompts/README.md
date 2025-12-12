# Chat API Prompt Templates

This directory contains Handlebars template files for all AI prompts used by the ALAN Chat API. Templates are loaded and rendered by the `PromptService`.

## Template Files

### chat-conversation.hbs
Prompt used for processing user chat messages with the agent.

**Variables:**
- `message` - The user's chat message

**Used by:** `ChatService.ProcessChatAsync()`

### chat-agent-instructions.hbs
System instructions for the AI agent in chat mode, defining its role and behavior.

**Variables:**
- None (static template)

**Used by:** `Program.cs` during AIAgent initialization

## Using Templates

### In Code

```csharp
// Inject IPromptService
public ChatService(
    AIAgent agent,
    ILogger<ChatService> logger,
    ILongTermMemoryService longTermMemory,
    IPromptService promptService)
{
    _promptService = promptService;
}

// Render a template
var prompt = _promptService.RenderTemplate("chat-conversation", new { message });
```

### Handlebars Syntax

Templates support standard Handlebars features:

**Variables:**
```handlebars
User message: {{message}}
```

**Conditionals:**
```handlebars
{{#if hasContext}}
  Context available
{{else}}
  No context
{{/if}}
```

**Loops:**
```handlebars
{{#each items}}
  - {{this.name}}
{{/each}}
```

## Editing Templates

Templates can be edited directly without recompiling code. The PromptService caches compiled templates, so changes require:

1. **Development:** Restart the ChatApi application to pick up changes
2. **Production:** Deploy updated template files alongside the application

## Best Practices

1. **Keep templates focused** - Each template should serve a single, clear purpose
2. **Document variables** - Use comments or this README to document expected variables
3. **Test changes** - Run unit tests after modifying templates
4. **Version control** - Always commit template changes with corresponding code changes
5. **Avoid complex logic** - Keep conditional logic simple; handle complexity in code

## Adding New Templates

1. Create a new `.hbs` file in this directory
2. Add variables using `{{variableName}}` syntax
3. The `.csproj` automatically copies all `.hbs` files to output
4. Use `_promptService.RenderTemplate("your-template", data)` in code
5. Add unit tests for the new template usage

## Template Caching

The PromptService caches compiled templates for performance. Cache behavior:

- Templates are compiled on first use
- Cached templates are reused for subsequent renders
- Cache persists for the lifetime of the service
- Use `ClearCache()` to force recompilation of all templates
- Use `ReloadTemplate(name)` to recompile a specific template

## Troubleshooting

**Template not found:**
- Verify the file exists in `src/ALAN.ChatApi/Prompts/`
- Check the file has `.hbs` extension
- Ensure template name matches file name (without extension)
- Verify the `.csproj` copies `.hbs` files to output

**Variables not rendering:**
- Check variable names match template placeholders exactly (case-sensitive)
- Verify data model properties are public
- Use anonymous objects or POCOs with public properties

**Syntax errors:**
- Validate Handlebars syntax (matching `{{` and `}}`)
- Check conditional blocks are properly closed (`{{/if}}`, `{{/each}}`)
- Test templates with unit tests
