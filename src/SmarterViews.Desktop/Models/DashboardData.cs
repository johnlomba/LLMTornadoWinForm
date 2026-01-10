using System.Collections.Generic;

namespace SmarterViews.Desktop.Models;

/// <summary>
/// Represents dashboard data for charts and KPIs
/// </summary>
public class DashboardData
{
    public string Title { get; set; } = string.Empty;
    public List<ChartDataPoint> DataPoints { get; set; } = new();
    public Dictionary<string, object> KPIs { get; set; } = new();
}

/// <summary>
/// Represents a single data point for charts
/// </summary>
public class ChartDataPoint
{
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime? Timestamp { get; set; }
}
