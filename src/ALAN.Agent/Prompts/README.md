# Prompt Templates

This directory contains Handlebars template files for all AI prompts used by the ALAN agent. Templates are loaded and rendered by the `PromptService`.

## Template Files

### agent-thinking.hbs
Main reasoning prompt for the autonomous agent loop. Used when the agent thinks about what to do next.

**Variables:**
- `directive` - Current directive/goal for the agent
- `memoryCount` - Number of memories loaded
- `memoryContext` - Formatted string of accumulated knowledge

**Used by:** `AutonomousAgent.ThinkAndActAsync()`

### action-execution.hbs
Prompt for executing a specific action planned by the agent.

**Variables:**
- `goal` - Current goal being pursued
- `reasoning` - Agent's reasoning from the planning phase
- `action` - Specific action to execute
- `extra` - Optional additional context

**Used by:** `AutonomousAgent.ParseAndExecuteActionAsync()`

### memory-consolidation.hbs
Prompt for analyzing memories and extracting learnings/patterns.

**Variables:**
- `memoryCount` - Number of memories to analyze
- `memoriesJson` - JSON serialized memory entries

**Used by:** `MemoryConsolidationService.ConsolidateMemoriesAsync()`

### agent-instructions.hbs
System instructions for the AI agent, defining its role and capabilities.

**Variables:**
- `projectUrl` - GitHub repository URL for self-improvement analysis

**Used by:** `Program.cs` during AIAgent initialization

## Using Templates

### In Code

```csharp
// Inject PromptService
public MyService(PromptService promptService)
{
    _promptService = promptService;
}

// Render a template
var prompt = _promptService.RenderTemplate("template-name", new
{
    variable1 = "value1",
    variable2 = 42,
    variable3 = someObject
});
```

### Handlebars Syntax

Templates support standard Handlebars features:

**Variables:**
```handlebars
Hello {{name}}!
```

**Conditionals:**
```handlebars
{{#if hasItems}}
  Items exist
{{else}}
  No items
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

1. **Development:** Restart the application to pick up changes
2. **Production:** Deploy updated template files alongside the application

For live development, you can use `PromptService.ClearCache()` or `PromptService.ReloadTemplate(name)` to force reloading.

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
5. Add unit tests in `PromptServiceTests.cs`

## Template Caching

The PromptService caches compiled templates for performance. Cache behavior:

- Templates are compiled on first use
- Cached templates are reused for subsequent renders
- Cache persists for the lifetime of the service
- Use `ClearCache()` to force recompilation of all templates
- Use `ReloadTemplate(name)` to recompile a specific template

## Troubleshooting

**Template not found:**
- Verify the file exists in `src/ALAN.Agent/Prompts/`
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
- Test templates with unit tests in `PromptServiceTests.cs`
