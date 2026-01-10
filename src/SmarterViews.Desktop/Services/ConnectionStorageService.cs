using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using SmarterViews.Desktop.Models;

namespace SmarterViews.Desktop.Services;

/// <summary>
/// Service for saving and loading database connections
/// </summary>
public class ConnectionStorageService
{
    private readonly string _storageDirectory;
    private const string ConnectionFileExtension = ".json";
    private const string ConnectionsFileName = "connections";

    public ConnectionStorageService(string? storageDirectory = null)
    {
        _storageDirectory = storageDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmarterViews",
            "Connections");

        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }
    }

    /// <summary>
    /// Saves a database connection to storage
    /// </summary>
    public async Task SaveConnectionAsync(DatabaseConnection connection, CancellationToken cancellationToken = default)
    {
        connection.UpdatedAt = DateTime.UtcNow;
        var filePath = GetConnectionFilePath(connection.Id);
        var json = JsonSerializer.Serialize(connection, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    /// <summary>
    /// Loads a database connection from storage
    /// </summary>
    public async Task<DatabaseConnection?> LoadConnectionAsync(string id, CancellationToken cancellationToken = default)
    {
        var filePath = GetConnectionFilePath(id);
        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        return JsonSerializer.Deserialize<DatabaseConnection>(json);
    }

    /// <summary>
    /// Loads all saved database connections
    /// </summary>
    public async Task<ObservableCollection<DatabaseConnection>> LoadAllConnectionsAsync(CancellationToken cancellationToken = default)
    {
        var connections = new ObservableCollection<DatabaseConnection>();
        var files = Directory.GetFiles(_storageDirectory, $"*{ConnectionFileExtension}");

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var connection = JsonSerializer.Deserialize<DatabaseConnection>(json);
                if (connection != null)
                {
                    connections.Add(connection);
                }
            }
            catch (JsonException)
            {
                // Skip invalid files
            }
        }

        return connections;
    }

    /// <summary>
    /// Deletes a database connection from storage
    /// </summary>
    public Task DeleteConnectionAsync(string id)
    {
        var filePath = GetConnectionFilePath(id);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        return Task.CompletedTask;
    }

    private string GetConnectionFilePath(string id)
    {
        return Path.Combine(_storageDirectory, $"{id}{ConnectionFileExtension}");
    }
}
