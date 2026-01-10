namespace SmarterViews.Desktop.Models;

/// <summary>
/// Represents an operator with plain English display name and SQL equivalent
/// </summary>
public class OperatorInfo
{
    public string Value { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] ApplicableTypes { get; set; } = [];

    public override string ToString() => DisplayName;

    public static List<OperatorInfo> AllOperators =>
    [
        new OperatorInfo 
        { 
            Value = "=", 
            DisplayName = "equals", 
            Description = "Value must match exactly",
            ApplicableTypes = ["string", "number", "date", "boolean"]
        },
        new OperatorInfo 
        { 
            Value = "!=", 
            DisplayName = "does not equal", 
            Description = "Value must be different",
            ApplicableTypes = ["string", "number", "date", "boolean"]
        },
        new OperatorInfo 
        { 
            Value = "LIKE", 
            DisplayName = "contains", 
            Description = "Value contains this text anywhere",
            ApplicableTypes = ["string"]
        },
        new OperatorInfo 
        { 
            Value = "STARTS_WITH", 
            DisplayName = "starts with", 
            Description = "Value begins with this text",
            ApplicableTypes = ["string"]
        },
        new OperatorInfo 
        { 
            Value = "ENDS_WITH", 
            DisplayName = "ends with", 
            Description = "Value ends with this text",
            ApplicableTypes = ["string"]
        },
        new OperatorInfo 
        { 
            Value = ">", 
            DisplayName = "is greater than", 
            Description = "Value is larger than specified",
            ApplicableTypes = ["number", "date"]
        },
        new OperatorInfo 
        { 
            Value = ">=", 
            DisplayName = "is at least", 
            Description = "Value is equal to or larger than specified",
            ApplicableTypes = ["number", "date"]
        },
        new OperatorInfo 
        { 
            Value = "<", 
            DisplayName = "is less than", 
            Description = "Value is smaller than specified",
            ApplicableTypes = ["number", "date"]
        },
        new OperatorInfo 
        { 
            Value = "<=", 
            DisplayName = "is at most", 
            Description = "Value is equal to or smaller than specified",
            ApplicableTypes = ["number", "date"]
        },
        new OperatorInfo 
        { 
            Value = "IS_NULL", 
            DisplayName = "is empty", 
            Description = "Field has no value",
            ApplicableTypes = ["string", "number", "date", "boolean"]
        },
        new OperatorInfo 
        { 
            Value = "IS_NOT_NULL", 
            DisplayName = "has a value", 
            Description = "Field is not empty",
            ApplicableTypes = ["string", "number", "date", "boolean"]
        }
    ];

    public static List<OperatorInfo> GetOperatorsForType(string dataType)
    {
        var normalizedType = NormalizeDataType(dataType);
        return AllOperators.Where(op => op.ApplicableTypes.Contains(normalizedType)).ToList();
    }

    private static string NormalizeDataType(string dataType)
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
