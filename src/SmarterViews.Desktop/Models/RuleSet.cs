using System.Collections.Generic;

namespace SmarterViews.Desktop.Models;

/// <summary>
/// Represents a collection of query rules
/// </summary>
public class RuleSet
{
    public string Condition { get; set; } = "AND";
    public List<QueryRule> Rules { get; set; } = new();
}
