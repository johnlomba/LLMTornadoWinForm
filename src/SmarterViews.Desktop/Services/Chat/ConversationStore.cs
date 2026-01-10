using System.IO;
using System.Text.Json;
using SmarterViews.Desktop.Models.Chat;
using LlmTornado.Code;

namespace SmarterViews.Desktop.Services.Chat;

/// <summary>
/// Service for persisting and loading conversations and settings to/from disk.
/// </summary>
public class ConversationStore
{
    private readonly string _conversationsDirectory;
    private readonly string _settingsFilePath;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public ConversationStore()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "SmarterViews.Desktop");
        _conversationsDirectory = Path.Combine(appFolder, "conversations");
        _settingsFilePath = Path.Combine(appFolder, "settings.json");
        
        Directory.CreateDirectory(_conversationsDirectory);
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
    
    public async Task SaveConversationAsync(ConversationModel conversation)
    {
        conversation.UpdatedAt = DateTime.UtcNow;
        var filePath = GetConversationFilePath(conversation.Id);
        var json = JsonSerializer.Serialize(conversation, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }
    
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
    
    public async Task<List<ConversationModel>> LoadAllConversationsAsync()
    {
        var conversations = new List<ConversationModel>();
        
        if (!Directory.Exists(_conversationsDirectory))
        {
            return conversations;
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
                    conversations.Add(conversation);
                }
            }
            catch
            {
                // Skip invalid files
            }
        }
        
        return conversations.OrderByDescending(c => c.UpdatedAt).ToList();
    }
    
    public Task DeleteConversationAsync(string id)
    {
        var filePath = GetConversationFilePath(id);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        return Task.CompletedTask;
    }
    
    public async Task SaveSettingsAsync(SettingsModel settings)
    {
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        await File.WriteAllTextAsync(_settingsFilePath, json);
    }
    
    public async Task<SettingsModel> LoadSettingsAsync()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new SettingsModel();
        }
        
        try
        {
            var json = await File.ReadAllTextAsync(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<SettingsModel>(json, _jsonOptions) ?? new SettingsModel();
            MigrateSettingsIfNeeded(settings);
            return settings;
        }
        catch
        {
            return new SettingsModel();
        }
    }
    
    private void MigrateSettingsIfNeeded(SettingsModel settings)
    {
        bool needsMigration = false;
        
        if (settings.ApiKeys.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(settings.OpenAiApiKey))
            {
                settings.ApiKeys[LLmProviders.OpenAi] = settings.OpenAiApiKey;
                needsMigration = true;
            }
            
            if (!string.IsNullOrWhiteSpace(settings.AzureApiKey))
            {
                settings.ApiKeys[LLmProviders.AzureOpenAi] = settings.AzureApiKey;
                needsMigration = true;
            }
        }
        
        if (needsMigration)
        {
            settings.OpenAiApiKey = null;
            settings.AzureApiKey = null;
            settings.UseAzure = null;
            
            _ = Task.Run(async () => await SaveSettingsAsync(settings));
        }
    }
    
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
