using System.IO;
using System.Text.Json;
using LlmTornado.WpfViews.Models;

namespace LlmTornado.WpfViews.Services;

/// <summary>
/// Service for persisting and loading conversations to/from disk.
/// </summary>
public class ConversationStore
{
    private readonly string _conversationsDirectory;
    private readonly string _settingsFilePath;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public ConversationStore()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "LlmTornado.WpfViews");
        _conversationsDirectory = Path.Combine(appFolder, "conversations");
        _settingsFilePath = Path.Combine(appFolder, "settings.json");
        
        // Ensure directories exist
        Directory.CreateDirectory(_conversationsDirectory);
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
    
    /// <summary>
    /// Saves a conversation to disk.
    /// </summary>
    public async Task SaveConversationAsync(ConversationModel conversation)
    {
        conversation.UpdatedAt = DateTime.UtcNow;
        var filePath = GetConversationFilePath(conversation.Id);
        var json = JsonSerializer.Serialize(conversation, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }
    
    /// <summary>
    /// Loads a conversation from disk.
    /// </summary>
    public async Task<ConversationModel?> LoadConversationAsync(string id)
    {
        var filePath = GetConversationFilePath(id);
        if (!File.Exists(filePath))
        {
            return null;
        }
        
        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<ConversationModel>(json, _jsonOptions);
    }
    
    /// <summary>
    /// Gets all saved conversation summaries (without full messages).
    /// </summary>
    public async Task<List<ConversationSummary>> GetConversationSummariesAsync()
    {
        var summaries = new List<ConversationSummary>();
        
        if (!Directory.Exists(_conversationsDirectory))
        {
            return summaries;
        }
        
        var files = Directory.GetFiles(_conversationsDirectory, "*.json");
        
        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var conversation = JsonSerializer.Deserialize<ConversationModel>(json, _jsonOptions);
                
                if (conversation != null)
                {
                    summaries.Add(new ConversationSummary
                    {
                        Id = conversation.Id,
                        Title = conversation.Title,
                        CreatedAt = conversation.CreatedAt,
                        UpdatedAt = conversation.UpdatedAt,
                        MessageCount = conversation.Messages.Count
                    });
                }
            }
            catch
            {
                // Skip invalid files
            }
        }
        
        return summaries.OrderByDescending(s => s.UpdatedAt).ToList();
    }
    
    /// <summary>
    /// Deletes a conversation from disk.
    /// </summary>
    public Task DeleteConversationAsync(string id)
    {
        var filePath = GetConversationFilePath(id);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Saves application settings.
    /// </summary>
    public async Task SaveSettingsAsync(SettingsModel settings)
    {
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        await File.WriteAllTextAsync(_settingsFilePath, json);
    }
    
    /// <summary>
    /// Loads application settings.
    /// </summary>
    public async Task<SettingsModel> LoadSettingsAsync()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new SettingsModel();
        }
        
        try
        {
            var json = await File.ReadAllTextAsync(_settingsFilePath);
            return JsonSerializer.Deserialize<SettingsModel>(json, _jsonOptions) ?? new SettingsModel();
        }
        catch
        {
            return new SettingsModel();
        }
    }
    
    /// <summary>
    /// Gets the full file path for a conversation.
    /// </summary>
    private string GetConversationFilePath(string id)
    {
        return Path.Combine(_conversationsDirectory, $"{id}.json");
    }
}

/// <summary>
/// Summary of a conversation for listing purposes.
/// </summary>
public class ConversationSummary
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int MessageCount { get; set; }
}

