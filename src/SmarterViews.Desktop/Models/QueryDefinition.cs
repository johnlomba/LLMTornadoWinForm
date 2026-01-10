using System.Collections.Generic;

namespace SmarterViews.Desktop.Models;

/// <summary>
/// Represents a saved query definition
/// </summary>
public class QueryDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public RuleSet? Rules { get; set; }
    public List<string> SelectedColumns { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
