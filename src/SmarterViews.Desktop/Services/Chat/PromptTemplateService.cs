using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using SmarterViews.Desktop.Models.Chat;

namespace SmarterViews.Desktop.Services.Chat;

/// <summary>
/// Service for managing system prompt templates.
/// Supports built-in templates, custom templates, and variable substitution.
/// </summary>
/// <remarks>
/// Templates support the following variables:
/// - {{database_type}} - The type of database (SQL Server, PostgreSQL, etc.)
/// - {{schema_info}} - Current database schema information
/// - {{table_name}} - Currently selected table name
/// - {{date}} - Current date
/// - {{user}} - Current user name
/// </remarks>
public class PromptTemplateService
{
    private readonly string _templatesFilePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private List<PromptTemplate> _templates = [];
    
    /// <summary>
    /// Event raised when templates are changed.
    /// </summary>
    public event Action? TemplatesChanged;
    
    public PromptTemplateService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appDataPath, "SmarterViews");
        _templatesFilePath = Path.Combine(appFolder, "templates.json");
        
        Directory.CreateDirectory(appFolder);
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
    
    /// <summary>
    /// Gets all available templates (built-in and custom).
    /// </summary>
    public async Task<List<PromptTemplate>> GetAllTemplatesAsync()
    {
        if (_templates.Count == 0)
        {
            await LoadTemplatesAsync();
        }
        
        return _templates;
    }
    
    /// <summary>
    /// Gets a template by ID.
    /// </summary>
    public async Task<PromptTemplate?> GetTemplateAsync(string id)
    {
        var templates = await GetAllTemplatesAsync();
        return templates.FirstOrDefault(t => t.Id == id);
    }
    
    /// <summary>
    /// Resolves template variables and returns the final prompt content.
    /// </summary>
    /// <param name="template">The template to resolve.</param>
    /// <param name="variables">Dictionary of variable names to values.</param>
    /// <returns>The prompt content with variables substituted.</returns>
    public static string ResolveTemplate(PromptTemplate template, Dictionary<string, string>? variables = null)
    {
        var content = template.Content;
        
        if (variables == null || variables.Count == 0)
        {
            return content;
        }
        
        // Replace variables in the format {{variable_name}}
        foreach (var (key, value) in variables)
        {
            var pattern = $"{{{{\\s*{Regex.Escape(key)}\\s*}}}}";
            content = Regex.Replace(content, pattern, value, RegexOptions.IgnoreCase);
        }
        
        return content;
    }
    
    /// <summary>
    /// Creates a system prompt with database context information.
    /// </summary>
    /// <param name="template">The base template.</param>
    /// <param name="databaseType">Type of database being queried.</param>
    /// <param name="schemaInfo">Schema information for context.</param>
    /// <param name="tableName">Currently selected table name.</param>
    /// <returns>The complete system prompt with context.</returns>
    public static string CreateDatabasePrompt(
        PromptTemplate template, 
        string? databaseType = null, 
        string? schemaInfo = null, 
        string? tableName = null)
    {
        var variables = new Dictionary<string, string>
        {
            ["database_type"] = databaseType ?? "SQL",
            ["schema_info"] = schemaInfo ?? "(no schema available)",
            ["table_name"] = tableName ?? "(all tables)",
            ["date"] = DateTime.Now.ToString("yyyy-MM-dd"),
            ["user"] = Environment.UserName
        };
        
        var resolvedPrompt = ResolveTemplate(template, variables);
        
        // Append schema information if available
        if (!string.IsNullOrWhiteSpace(schemaInfo))
        {
            resolvedPrompt += $"\n\n## Database Schema Information:\n```\n{schemaInfo}\n```";
        }
        
        return resolvedPrompt;
    }
    
    /// <summary>
    /// Adds a custom template.
    /// </summary>
    public async Task AddTemplateAsync(PromptTemplate template)
    {
        template.IsBuiltIn = false;
        template.CreatedAt = DateTime.UtcNow;
        _templates.Add(template);
        await SaveCustomTemplatesAsync();
        TemplatesChanged?.Invoke();
    }
    
    /// <summary>
    /// Updates an existing custom template.
    /// </summary>
    public async Task UpdateTemplateAsync(PromptTemplate template)
    {
        if (template.IsBuiltIn)
        {
            throw new InvalidOperationException("Cannot modify built-in templates.");
        }
        
        var existing = _templates.FirstOrDefault(t => t.Id == template.Id);
        if (existing != null)
        {
            var index = _templates.IndexOf(existing);
            _templates[index] = template;
            await SaveCustomTemplatesAsync();
            TemplatesChanged?.Invoke();
        }
    }
    
    /// <summary>
    /// Deletes a custom template.
    /// </summary>
    public async Task DeleteTemplateAsync(string id)
    {
        var template = _templates.FirstOrDefault(t => t.Id == id);
        if (template != null)
        {
            if (template.IsBuiltIn)
            {
                throw new InvalidOperationException("Cannot delete built-in templates.");
            }
            
            _templates.Remove(template);
            await SaveCustomTemplatesAsync();
            TemplatesChanged?.Invoke();
        }
    }
    
    /// <summary>
    /// Duplicates a template (including built-in templates).
    /// </summary>
    /// <param name="sourceId">ID of the template to duplicate.</param>
    /// <param name="newName">Name for the duplicated template.</param>
    /// <returns>The new duplicated template.</returns>
    public async Task<PromptTemplate?> DuplicateTemplateAsync(string sourceId, string? newName = null)
    {
        var source = await GetTemplateAsync(sourceId);
        if (source == null) return null;
        
        var duplicate = new PromptTemplate
        {
            Id = Guid.NewGuid().ToString(),
            Name = newName ?? $"{source.Name} (Copy)",
            Content = source.Content,
            Description = source.Description,
            IsBuiltIn = false,
            CreatedAt = DateTime.UtcNow
        };
        
        await AddTemplateAsync(duplicate);
        return duplicate;
    }
    
    /// <summary>
    /// Exports templates to a JSON string.
    /// </summary>
    /// <param name="includeBuiltIn">Whether to include built-in templates.</param>
    /// <returns>JSON string of templates.</returns>
    public string ExportTemplates(bool includeBuiltIn = false)
    {
        var templatesToExport = includeBuiltIn 
            ? _templates 
            : _templates.Where(t => !t.IsBuiltIn).ToList();
        
        return JsonSerializer.Serialize(templatesToExport, _jsonOptions);
    }
    
    /// <summary>
    /// Imports templates from a JSON string.
    /// </summary>
    /// <param name="json">JSON string containing templates.</param>
    /// <returns>Number of templates imported.</returns>
    public async Task<int> ImportTemplatesAsync(string json)
    {
        var imported = JsonSerializer.Deserialize<List<PromptTemplate>>(json, _jsonOptions);
        if (imported == null) return 0;
        
        var count = 0;
        foreach (var template in imported)
        {
            // Skip if it's marked as built-in (can't import built-in templates)
            template.IsBuiltIn = false;
            
            // Generate new ID to avoid conflicts
            template.Id = Guid.NewGuid().ToString();
            template.CreatedAt = DateTime.UtcNow;
            
            _templates.Add(template);
            count++;
        }
        
        if (count > 0)
        {
            await SaveCustomTemplatesAsync();
            TemplatesChanged?.Invoke();
        }
        
        return count;
    }
    
    /// <summary>
    /// Loads all templates from disk and merges with built-in templates.
    /// </summary>
    private async Task LoadTemplatesAsync()
    {
        _templates = PromptTemplate.GetBuiltInTemplates();
        
        if (File.Exists(_templatesFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_templatesFilePath);
                var customTemplates = JsonSerializer.Deserialize<List<PromptTemplate>>(json, _jsonOptions);
                
                if (customTemplates != null)
                {
                    _templates.AddRange(customTemplates);
                }
            }
            catch
            {
                // Ignore errors loading custom templates
            }
        }
    }
    
    /// <summary>
    /// Saves custom templates to disk.
    /// </summary>
    private async Task SaveCustomTemplatesAsync()
    {
        var customTemplates = _templates.Where(t => !t.IsBuiltIn).ToList();
        var json = JsonSerializer.Serialize(customTemplates, _jsonOptions);
        await File.WriteAllTextAsync(_templatesFilePath, json);
    }
}
