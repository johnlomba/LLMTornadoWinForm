using CommunityToolkit.Mvvm.ComponentModel;
using static SmarterViews.Desktop.Services.SchemaService;

namespace SmarterViews.Desktop.Models;

/// <summary>
/// Represents a single query rule/condition with observable properties for UI binding
/// </summary>
public partial class QueryRule : ObservableObject
{
    [ObservableProperty]
    private string _field = string.Empty;

    [ObservableProperty]
    private ColumnInfo? _selectedColumn;

    [ObservableProperty]
    private OperatorInfo? _selectedOperator;

    [ObservableProperty]
    private string _operator = "=";

    [ObservableProperty]
    private object? _value;

    [ObservableProperty]
    private string _stringValue = string.Empty;

    [ObservableProperty]
    private DateTime? _dateValue;

    [ObservableProperty]
    private double? _numericValue;

    [ObservableProperty]
    private bool _booleanValue;

    [ObservableProperty]
    private List<OperatorInfo> _availableOperators = OperatorInfo.AllOperators;

    [ObservableProperty]
    private string _dataType = "string";

    partial void OnSelectedColumnChanged(ColumnInfo? value)
    {
        if (value != null)
        {
            Field = value.Name;
            DataType = GetNormalizedDataType(value.DataType);
            AvailableOperators = OperatorInfo.GetOperatorsForType(value.DataType);
            
            // Reset operator to first available if current is not applicable
            if (SelectedOperator == null || !AvailableOperators.Contains(SelectedOperator))
            {
                SelectedOperator = AvailableOperators.FirstOrDefault();
            }
        }
    }

    partial void OnSelectedOperatorChanged(OperatorInfo? value)
    {
        if (value != null)
        {
            Operator = value.Value;
        }
    }

    /// <summary>
    /// Gets the effective value based on the data type
    /// </summary>
    public object? GetEffectiveValue()
    {
        return DataType switch
        {
            "date" => DateValue,
            "number" => NumericValue,
            "boolean" => BooleanValue,
            _ => StringValue
        };
    }

    private static string GetNormalizedDataType(string dataType)
    {
        var lower = dataType.ToLowerInvariant();
        
        if (lower.Contains("int") || lower.Contains("decimal") || lower.Contains("numeric") || 
            lower.Contains("float") || lower.Contains("double") || lower.Contains("money") ||
            lower.Contains("real") || lower.Contains("bigint") || lower.Contains("smallint") ||
            lower.Contains("tinyint"))
            return "number";
        
        if (lower.Contains("date") || lower.Contains("time"))
            return "date";
        
        if (lower.Contains("bit") || lower.Contains("bool"))
            return "boolean";
        
        return "string";
    }
}
