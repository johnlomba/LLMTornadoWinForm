namespace SmarterViews.Desktop.Models;

/// <summary>
/// Represents a database connection configuration
/// </summary>
public class DatabaseConnection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseType { get; set; } = "SqlServer"; // SqlServer, MySql, PostgreSQL, etc.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDefault { get; set; }
}
