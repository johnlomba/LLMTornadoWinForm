using System.IO;
using System.Text.Json;
using SmarterViews.Desktop.Models.Chat;

namespace SmarterViews.Desktop.Services.Chat;

/// <summary>
/// Service for managing system prompt templates.
/// </summary>
public class PromptTemplateService
{
    private readonly string _templatesFilePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private List<PromptTemplate> _templates = [];
    
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
    /// Adds a custom template.
    /// </summary>
    public async Task AddTemplateAsync(PromptTemplate template)
    {
        template.IsBuiltIn = false;
        template.CreatedAt = DateTime.UtcNow;
        _templates.Add(template);
        await SaveCustomTemplatesAsync();
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
        }
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
