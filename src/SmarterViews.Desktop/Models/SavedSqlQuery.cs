namespace SmarterViews.Desktop.Models;

/// <summary>
/// Represents a saved SQL query from AI generation
/// </summary>
public class SavedSqlQuery
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string SqlText { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DatabaseConnection { get; set; }
    public string? PromptUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
