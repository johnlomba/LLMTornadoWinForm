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
            var settings = JsonSerializer.Deserialize<SettingsModel>(json, _jsonOptions) ?? new SettingsModel();
            
            // Migrate from old format if needed
            MigrateSettingsIfNeeded(settings);
            
            return settings;
        }
        catch
        {
            return new SettingsModel();
        }
    }
    
    /// <summary>
    /// Migrates settings from old format to new format.
    /// </summary>
    private void MigrateSettingsIfNeeded(SettingsModel settings)
    {
        bool needsMigration = false;
        
        // Check if we have legacy properties but no new ApiKeys dictionary populated
        if (settings.ApiKeys.Count == 0)
        {
            // Migrate OpenAI key
            if (!string.IsNullOrWhiteSpace(settings.OpenAiApiKey))
            {
                settings.ApiKeys[LlmTornado.Code.LLmProviders.OpenAi] = settings.OpenAiApiKey;
                needsMigration = true;
            }
            
            // Migrate Azure key
            if (!string.IsNullOrWhiteSpace(settings.AzureApiKey))
            {
                settings.ApiKeys[LlmTornado.Code.LLmProviders.AzureOpenAi] = settings.AzureApiKey;
                needsMigration = true;
            }
        }
        
        // If migration occurred, clear legacy properties and save
        if (needsMigration)
        {
            settings.OpenAiApiKey = null;
            settings.AzureApiKey = null;
            settings.UseAzure = null;
            
            // Save migrated settings
            _ = Task.Run(async () => await SaveSettingsAsync(settings));
        }
    }
    
    /// <summary>
    /// Renames a conversation.
    /// </summary>
    /// <param name="id">Conversation ID.</param>
    /// <param name="newTitle">New title for the conversation.</param>
    public async Task RenameConversationAsync(string id, string newTitle)
    {
        var conversation = await LoadConversationAsync(id);
        if (conversation != null)
        {
            conversation.Title = newTitle;
            conversation.UpdatedAt = DateTime.UtcNow;
            await SaveConversationAsync(conversation);
        }
    }
    
    /// <summary>
    /// Archives a conversation (moves to archive folder).
    /// </summary>
    /// <param name="id">Conversation ID to archive.</param>
    public async Task ArchiveConversationAsync(string id)
    {
        var archiveDirectory = Path.Combine(_conversationsDirectory, "archived");
        Directory.CreateDirectory(archiveDirectory);
        
        var sourcePath = GetConversationFilePath(id);
        if (File.Exists(sourcePath))
        {
            var destPath = Path.Combine(archiveDirectory, $"{id}.json");
            await Task.Run(() => File.Move(sourcePath, destPath, overwrite: true));
        }
    }
    
    /// <summary>
    /// Gets archived conversation summaries.
    /// </summary>
    public async Task<List<ConversationSummary>> GetArchivedConversationSummariesAsync()
    {
        var archiveDirectory = Path.Combine(_conversationsDirectory, "archived");
        if (!Directory.Exists(archiveDirectory))
        {
            return [];
        }
        
        var summaries = new List<ConversationSummary>();
        var files = Directory.GetFiles(archiveDirectory, "*.json");
        
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
                        MessageCount = conversation.Messages.Count,
                        IsArchived = true
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
    /// Restores an archived conversation.
    /// </summary>
    /// <param name="id">Conversation ID to restore.</param>
    public async Task RestoreConversationAsync(string id)
    {
        var archiveDirectory = Path.Combine(_conversationsDirectory, "archived");
        var sourcePath = Path.Combine(archiveDirectory, $"{id}.json");
        
        if (File.Exists(sourcePath))
        {
            var destPath = GetConversationFilePath(id);
            await Task.Run(() => File.Move(sourcePath, destPath, overwrite: true));
        }
    }
    
    /// <summary>
    /// Searches conversations by title or content.
    /// </summary>
    /// <param name="searchTerm">Term to search for.</param>
    /// <returns>List of matching conversation summaries.</returns>
    public async Task<List<ConversationSummary>> SearchConversationsAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetConversationSummariesAsync();
        }
        
        var allSummaries = new List<ConversationSummary>();
        
        if (!Directory.Exists(_conversationsDirectory))
        {
            return allSummaries;
        }
        
        var files = Directory.GetFiles(_conversationsDirectory, "*.json");
        var searchLower = searchTerm.ToLowerInvariant();
        
        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var conversation = JsonSerializer.Deserialize<ConversationModel>(json, _jsonOptions);
                
                if (conversation != null)
                {
                    // Search in title
                    var matchesTitle = conversation.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
                    
                    // Search in message content
                    var matchesContent = conversation.Messages.Any(m => 
                        m.Content.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
                    
                    if (matchesTitle || matchesContent)
                    {
                        allSummaries.Add(new ConversationSummary
                        {
                            Id = conversation.Id,
                            Title = conversation.Title,
                            CreatedAt = conversation.CreatedAt,
                            UpdatedAt = conversation.UpdatedAt,
                            MessageCount = conversation.Messages.Count,
                            SearchMatchType = matchesTitle ? SearchMatchType.Title : SearchMatchType.Content
                        });
                    }
                }
            }
            catch
            {
                // Skip invalid files
            }
        }
        
        return allSummaries.OrderByDescending(s => s.UpdatedAt).ToList();
    }
    
    /// <summary>
    /// Exports a conversation to a file.
    /// </summary>
    /// <param name="id">Conversation ID.</param>
    /// <param name="exportPath">Path to export to.</param>
    /// <param name="format">Export format (json, markdown).</param>
    public async Task ExportConversationAsync(string id, string exportPath, string format = "json")
    {
        var conversation = await LoadConversationAsync(id);
        if (conversation == null) return;
        
        string content;
        
        if (format.Equals("markdown", StringComparison.OrdinalIgnoreCase) || 
            format.Equals("md", StringComparison.OrdinalIgnoreCase))
        {
            content = ConvertToMarkdown(conversation);
        }
        else
        {
            content = JsonSerializer.Serialize(conversation, _jsonOptions);
        }
        
        await File.WriteAllTextAsync(exportPath, content);
    }
    
    private static string ConvertToMarkdown(ConversationModel conversation)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {conversation.Title}");
        sb.AppendLine();
        sb.AppendLine($"*Created: {conversation.CreatedAt:yyyy-MM-dd HH:mm}*");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        
        foreach (var message in conversation.Messages)
        {
            var roleEmoji = message.Role switch
            {
                MessageRole.User => "👤 **User**",
                MessageRole.Assistant => "🤖 **Assistant**",
                MessageRole.System => "⚙️ **System**",
                _ => "**Unknown**"
            };
            
            sb.AppendLine($"### {roleEmoji}");
            sb.AppendLine();
            sb.AppendLine(message.Content);
            sb.AppendLine();
        }
        
        return sb.ToString();
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
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Conversation title.
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// When the conversation was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// When the conversation was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// Number of messages in the conversation.
    /// </summary>
    public int MessageCount { get; set; }
    
    /// <summary>
    /// Whether this conversation is archived.
    /// </summary>
    public bool IsArchived { get; set; }
    
    /// <summary>
    /// Type of search match (for search results).
    /// </summary>
    public SearchMatchType SearchMatchType { get; set; } = SearchMatchType.None;
    
    /// <summary>
    /// Human-readable time since last update.
    /// </summary>
    public string TimeAgo
    {
        get
        {
            var elapsed = DateTime.UtcNow - UpdatedAt;
            if (elapsed.TotalMinutes < 1) return "just now";
            if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
            if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
            if (elapsed.TotalDays < 7) return $"{(int)elapsed.TotalDays}d ago";
            return UpdatedAt.ToString("MMM d");
        }
    }
}

/// <summary>
/// Type of search match for conversation search.
/// </summary>
public enum SearchMatchType
{
    /// <summary>
    /// No search was performed.
    /// </summary>
    None,
    
    /// <summary>
    /// Match found in title.
    /// </summary>
    Title,
    
    /// <summary>
    /// Match found in message content.
    /// </summary>
    Content
}

