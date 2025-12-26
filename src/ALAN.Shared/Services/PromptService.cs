using HandlebarsDotNet;
using Microsoft.Extensions.Logging;

namespace ALAN.Shared.Services;

/// <summary>
/// Service for loading and rendering Handlebars prompt templates.
/// Provides centralized prompt management with template-based rendering.
/// </summary>
public class PromptService(ILogger<PromptService> logger, string? promptsDirectory = null) : IPromptService
{
    private readonly string _promptsDirectory = InitializeDirectory(logger, promptsDirectory);
    private readonly Dictionary<string, HandlebarsTemplate<object, object>> _compiledTemplates = [];
    private readonly IHandlebars _handlebars = Handlebars.Create();

    private static string InitializeDirectory(ILogger<PromptService> logger, string? promptsDirectory)
    {
        var directory = promptsDirectory ?? Path.Combine(AppContext.BaseDirectory, "Prompts");
        
        if (!Directory.Exists(directory))
        {
            logger.LogWarning("Prompts directory not found: {Directory}", directory);
            Directory.CreateDirectory(directory);
        }

        logger.LogInformation("Prompt service initialized with directory: {Directory}", directory);
        return directory;
    }

    /// <summary>
    /// Renders a template with the provided data model.
    /// </summary>
    /// <param name="templateName">Name of the template file (without .hbs extension)</param>
    /// <param name="data">Data model to render the template with</param>
    /// <returns>Rendered prompt text</returns>
    public string RenderTemplate(string templateName, object data)
    {
        try
        {
            var template = GetOrCompileTemplate(templateName);
            var result = template(data);
            logger.LogTrace("Rendered template {TemplateName} with {Length} characters", 
                templateName, result.Length);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to render template {TemplateName}", templateName);
            throw new InvalidOperationException($"Failed to render template '{templateName}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets a compiled template from cache or compiles it if not cached.
    /// </summary>
    private HandlebarsTemplate<object, object> GetOrCompileTemplate(string templateName)
    {
        if (_compiledTemplates.TryGetValue(templateName, out var cachedTemplate))
        {
            return cachedTemplate;
        }

        var templatePath = Path.Combine(_promptsDirectory, $"{templateName}.hbs");
        
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Template file not found: {templatePath}");
        }

        logger.LogInformation("Compiling template: {TemplateName} from {Path}", 
            templateName, templatePath);

        var templateContent = File.ReadAllText(templatePath);
        var template = _handlebars.Compile(templateContent);
        
        _compiledTemplates[templateName] = template;
        
        return template;
    }

    /// <summary>
    /// Clears the template cache, forcing recompilation on next use.
    /// Useful for development when templates are being edited.
    /// </summary>
    public void ClearCache()
    {
        _compiledTemplates.Clear();
        logger.LogInformation("Template cache cleared");
    }

    /// <summary>
    /// Reloads a specific template from disk.
    /// </summary>
    public void ReloadTemplate(string templateName)
    {
        if (_compiledTemplates.Remove(templateName))
        {
            logger.LogInformation("Template {TemplateName} removed from cache for reload", templateName);
        }
    }

    /// <summary>
    /// Lists all available template files in the prompts directory.
    /// </summary>
    public List<string> GetAvailableTemplates()
    {
        if (!Directory.Exists(_promptsDirectory))
        {
            return [];
        }

        return Directory.GetFiles(_promptsDirectory, "*.hbs")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .ToList();
    }
}
