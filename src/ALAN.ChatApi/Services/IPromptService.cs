namespace ALAN.ChatApi.Services;

/// <summary>
/// Interface for prompt template loading and rendering service.
/// </summary>
public interface IPromptService
{
    /// <summary>
    /// Renders a template with the provided data model.
    /// </summary>
    /// <param name="templateName">Name of the template file (without .hbs extension)</param>
    /// <param name="data">Data model to render the template with</param>
    /// <returns>Rendered prompt text</returns>
    string RenderTemplate(string templateName, object data);

    /// <summary>
    /// Clears the template cache, forcing recompilation on next use.
    /// Useful for development when templates are being edited.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Reloads a specific template from disk.
    /// </summary>
    void ReloadTemplate(string templateName);

    /// <summary>
    /// Lists all available template files in the prompts directory.
    /// </summary>
    List<string> GetAvailableTemplates();
}
