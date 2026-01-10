using System.IO;
using System.Text.Json;
using SmarterViews.Desktop.Models.Chat;
using LlmTornado.Code;

namespace SmarterViews.Desktop.Services.Chat;

/// <summary>
/// Service for persisting and loading conversations and settings to/from disk.
/// Provides comprehensive conversation management including search, archive, and export capabilities.
/// </summary>
/// <remarks>
/// Data is stored in the user's application data folder under SmarterViews.Desktop.
/// Conversations are stored as individual JSON files for easy backup and migration.
/// Archived conversations are moved to a separate archive subdirectory.
/// </remarks>
public class ConversationStore
{
    private readonly string _conversationsDirectory;
    private readonly string _archiveDirectory;
    private readonly string _settingsFilePath;
    private readonly string _exportDirectory;
    private readonly JsonSerializerOptions _jsonOptions;
    
    /// <summary>
    /// Event raised when a conversation is saved.
    /// </summary>
    public event Action<string>? ConversationSaved;
    
    /// <summary>
    /// Event raised when a conversation is deleted.
    /// </summary>
    public event Action<string>? ConversationDeleted;
    
    /// <summary>
    /// Event raised when a conversation is archived.
    /// </summary>
    public event Action<string>? ConversationArchived;
    
    public ConversationStore()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "SmarterViews.Desktop");
        _conversationsDirectory = Path.Combine(appFolder, "conversations");
        _archiveDirectory = Path.Combine(appFolder, "conversations", "archive");
        _exportDirectory = Path.Combine(appFolder, "exports");
        _settingsFilePath = Path.Combine(appFolder, "settings.json");
        
        Directory.CreateDirectory(_conversationsDirectory);
        Directory.CreateDirectory(_archiveDirectory);
        Directory.CreateDirectory(_exportDirectory);
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
    
    /// <summary>
    /// Saves a conversation to disk. Updates the UpdatedAt timestamp automatically.
    /// </summary>
    /// <param name="conversation">The conversation to save.</param>
    public async Task SaveConversationAsync(ConversationModel conversation)
    {
        conversation.UpdatedAt = DateTime.UtcNow;
        var filePath = GetConversationFilePath(conversation.Id);
        var json = JsonSerializer.Serialize(conversation, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
        ConversationSaved?.Invoke(conversation.Id);
    }
    
    /// <summary>
    /// Loads a conversation by its ID.
    /// </summary>
    /// <param name="id">The conversation ID.</param>
    /// <returns>The conversation if found, null otherwise.</returns>
    public async Task<ConversationModel?> LoadConversationAsync(string id)
    {
        var filePath = GetConversationFilePath(id);
        if (!File.Exists(filePath))
        {
            // Also check archive directory
            var archivePath = GetArchiveFilePath(id);
            if (!File.Exists(archivePath))
                return null;
            filePath = archivePath;
        }
        
        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<ConversationModel>(json, _jsonOptions);
    }
    
    /// <summary>
    /// Renames a conversation by updating its title.
    /// </summary>
    /// <param name="id">The conversation ID.</param>
    /// <param name="newTitle">The new title for the conversation.</param>
    /// <returns>True if the conversation was renamed successfully.</returns>
    public async Task<bool> RenameConversationAsync(string id, string newTitle)
    {
        var conversation = await LoadConversationAsync(id);
        if (conversation == null) return false;
        
        conversation.Title = newTitle;
        await SaveConversationAsync(conversation);
        return true;
    }
    
    /// <summary>
    /// Archives a conversation by moving it to the archive directory.
    /// Archived conversations are excluded from the main list but can still be loaded.
    /// </summary>
    /// <param name="id">The conversation ID to archive.</param>
    /// <returns>True if the conversation was archived successfully.</returns>
    public Task<bool> ArchiveConversationAsync(string id)
    {
        var sourcePath = GetConversationFilePath(id);
        if (!File.Exists(sourcePath))
            return Task.FromResult(false);
        
        var destPath = GetArchiveFilePath(id);
        File.Move(sourcePath, destPath, overwrite: true);
        ConversationArchived?.Invoke(id);
        return Task.FromResult(true);
    }
    
    /// <summary>
    /// Restores an archived conversation to the active conversations.
    /// </summary>
    /// <param name="id">The conversation ID to restore.</param>
    /// <returns>True if the conversation was restored successfully.</returns>
    public Task<bool> RestoreConversationAsync(string id)
    {
        var sourcePath = GetArchiveFilePath(id);
        if (!File.Exists(sourcePath))
            return Task.FromResult(false);
        
        var destPath = GetConversationFilePath(id);
        File.Move(sourcePath, destPath, overwrite: true);
        return Task.FromResult(true);
    }
    
    /// <summary>
    /// Searches conversations by title and message content.
    /// </summary>
    /// <param name="searchQuery">The search query string.</param>
    /// <param name="includeArchived">Whether to include archived conversations in search results.</param>
    /// <returns>List of conversation summaries matching the search query.</returns>
    public async Task<List<ConversationSummary>> SearchConversationsAsync(string searchQuery, bool includeArchived = false)
    {
        var results = new List<ConversationSummary>();
        var normalizedQuery = searchQuery.ToLowerInvariant();
        
        var directories = new List<string> { _conversationsDirectory };
        if (includeArchived)
            directories.Add(_archiveDirectory);
        
        foreach (var directory in directories)
        {
            if (!Directory.Exists(directory)) continue;
            
            var files = Directory.GetFiles(directory, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var conversation = JsonSerializer.Deserialize<ConversationModel>(json, _jsonOptions);
                    
                    if (conversation != null && MatchesSearch(conversation, normalizedQuery))
                    {
                        results.Add(new ConversationSummary
                        {
                            Id = conversation.Id,
                            Title = conversation.Title,
                            CreatedAt = conversation.CreatedAt,
                            UpdatedAt = conversation.UpdatedAt,
                            MessageCount = conversation.Messages.Count,
                            IsArchived = directory == _archiveDirectory
                        });
                    }
                }
                catch
                {
                    // Skip invalid files
                }
            }
        }
        
        return results.OrderByDescending(s => s.UpdatedAt).ToList();
    }
    
    /// <summary>
    /// Exports a conversation to a Markdown file.
    /// </summary>
    /// <param name="id">The conversation ID to export.</param>
    /// <param name="destinationPath">Optional destination path. If null, exports to default exports folder.</param>
    /// <returns>The path to the exported file, or null if export failed.</returns>
    public async Task<string?> ExportConversationAsync(string id, string? destinationPath = null)
    {
        var conversation = await LoadConversationAsync(id);
        if (conversation == null) return null;
        
        var markdown = ConvertToMarkdown(conversation);
        var fileName = SanitizeFileName($"{conversation.Title}_{conversation.Id[..8]}.md");
        var filePath = destinationPath ?? Path.Combine(_exportDirectory, fileName);
        
        await File.WriteAllTextAsync(filePath, markdown);
        return filePath;
    }
    
    /// <summary>
    /// Exports a conversation to JSON format.
    /// </summary>
    /// <param name="id">The conversation ID to export.</param>
    /// <param name="destinationPath">Optional destination path.</param>
    /// <returns>The path to the exported file, or null if export failed.</returns>
    public async Task<string?> ExportConversationAsJsonAsync(string id, string? destinationPath = null)
    {
        var conversation = await LoadConversationAsync(id);
        if (conversation == null) return null;
        
        var json = JsonSerializer.Serialize(conversation, _jsonOptions);
        var fileName = SanitizeFileName($"{conversation.Title}_{conversation.Id[..8]}.json");
        var filePath = destinationPath ?? Path.Combine(_exportDirectory, fileName);
        
        await File.WriteAllTextAsync(filePath, json);
        return filePath;
    }
    
    private bool MatchesSearch(ConversationModel conversation, string normalizedQuery)
    {
        // Check title
        if (conversation.Title.ToLowerInvariant().Contains(normalizedQuery))
            return true;
        
        // Check message content
        foreach (var message in conversation.Messages)
        {
            if (message.Content?.ToLowerInvariant().Contains(normalizedQuery) == true)
                return true;
        }
        
        return false;
    }
    
    private string ConvertToMarkdown(ConversationModel conversation)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {conversation.Title}");
        sb.AppendLine();
        sb.AppendLine($"*Created: {conversation.CreatedAt:yyyy-MM-dd HH:mm} | Updated: {conversation.UpdatedAt:yyyy-MM-dd HH:mm}*");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        
        foreach (var message in conversation.Messages)
        {
            var role = message.Role.ToString().ToUpperInvariant() ?? "UNKNOWN";
            sb.AppendLine($"## {role}");
            sb.AppendLine();
            sb.AppendLine(message.Content ?? "(empty)");
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    private string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }
    
    private string GetArchiveFilePath(string id)
    {
        return Path.Combine(_archiveDirectory, $"{id}.json");
    }
    
    /// <summary>
    /// Gets lightweight summaries of all conversations for listing purposes.
    /// Does not include archived conversations.
    /// </summary>
    /// <returns>List of conversation summaries ordered by most recently updated.</returns>
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
                        MessageCount = conversation.Messages.Count,
                        IsArchived = false
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
    /// Gets summaries of all archived conversations.
    /// </summary>
    /// <returns>List of archived conversation summaries.</returns>
    public async Task<List<ConversationSummary>> GetArchivedConversationSummariesAsync()
    {
        var summaries = new List<ConversationSummary>();
        
        if (!Directory.Exists(_archiveDirectory))
        {
            return summaries;
        }
        
        var files = Directory.GetFiles(_archiveDirectory, "*.json");
        
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
    /// Loads all conversations from disk (excluding archived).
    /// </summary>
    /// <returns>List of all conversations ordered by most recently updated.</returns>
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
    
    /// <summary>
    /// Deletes a conversation permanently from disk.
    /// </summary>
    /// <param name="id">The conversation ID to delete.</param>
    public Task DeleteConversationAsync(string id)
    {
        var filePath = GetConversationFilePath(id);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            ConversationDeleted?.Invoke(id);
        }
        
        // Also check archive
        var archivePath = GetArchiveFilePath(id);
        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
            ConversationDeleted?.Invoke(id);
        }
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Gets the count of all conversations including archived.
    /// </summary>
    public int GetTotalConversationCount()
    {
        var activeCount = Directory.Exists(_conversationsDirectory) 
            ? Directory.GetFiles(_conversationsDirectory, "*.json").Length 
            : 0;
        var archivedCount = Directory.Exists(_archiveDirectory)
            ? Directory.GetFiles(_archiveDirectory, "*.json").Length
            : 0;
        return activeCount + archivedCount;
    }
    
    /// <summary>
    /// Saves the application settings to disk.
    /// </summary>
    /// <param name="settings">The settings to save.</param>
    public async Task SaveSettingsAsync(SettingsModel settings)
    {
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        await File.WriteAllTextAsync(_settingsFilePath, json);
    }
    
    /// <summary>
    /// Loads the application settings from disk.
    /// Returns default settings if the file doesn't exist or is invalid.
    /// </summary>
    /// <returns>The loaded settings or default settings.</returns>
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
/// Contains lightweight metadata without the full message content.
/// </summary>
public class ConversationSummary
{
    /// <summary>Unique identifier for the conversation.</summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>Display title of the conversation.</summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>When the conversation was created.</summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>When the conversation was last updated.</summary>
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>Total number of messages in the conversation.</summary>
    public int MessageCount { get; set; }
    
    /// <summary>Whether this conversation is in the archive.</summary>
    public bool IsArchived { get; set; }
    
    /// <summary>
    /// Returns a preview snippet for display purposes.
    /// </summary>
    public string GetPreview(int maxLength = 100)
    {
        if (Title.Length <= maxLength)
            return Title;
        return Title[..(maxLength - 3)] + "...";
    }
}
