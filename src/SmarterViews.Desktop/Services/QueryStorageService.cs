using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using SmarterViews.Desktop.Models;

namespace SmarterViews.Desktop.Services;

/// <summary>
/// Service for saving and loading query definitions
/// </summary>
public class QueryStorageService
{
    private readonly string _storageDirectory;
    private const string QueryFileExtension = ".json";

    public QueryStorageService(string? storageDirectory = null)
    {
        _storageDirectory = storageDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmarterViews",
            "Queries");

        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }
    }

    /// <summary>
    /// Saves a query definition to storage
    /// </summary>
    public async Task SaveQueryAsync(QueryDefinition query, CancellationToken cancellationToken = default)
    {
        query.UpdatedAt = DateTime.UtcNow;
        var filePath = GetQueryFilePath(query.Id);
        var json = JsonSerializer.Serialize(query, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    /// <summary>
    /// Loads a query definition from storage
    /// </summary>
    public async Task<QueryDefinition?> LoadQueryAsync(string id, CancellationToken cancellationToken = default)
    {
        var filePath = GetQueryFilePath(id);
        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        return JsonSerializer.Deserialize<QueryDefinition>(json);
    }

    /// <summary>
    /// Loads all saved query definitions
    /// </summary>
    public async Task<ObservableCollection<QueryDefinition>> LoadAllQueriesAsync(CancellationToken cancellationToken = default)
    {
        var queries = new ObservableCollection<QueryDefinition>();
        var files = Directory.GetFiles(_storageDirectory, $"*{QueryFileExtension}");

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var query = JsonSerializer.Deserialize<QueryDefinition>(json);
                if (query != null)
                {
                    queries.Add(query);
                }
            }
            catch (JsonException)
            {
                // Skip invalid files
            }
        }

        return queries;
    }

    /// <summary>
    /// Deletes a query definition from storage
    /// </summary>
    public Task DeleteQueryAsync(string id)
    {
        var filePath = GetQueryFilePath(id);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        return Task.CompletedTask;
    }

    private string GetQueryFilePath(string id)
    {
        return Path.Combine(_storageDirectory, $"{id}{QueryFileExtension}");
    }
}
