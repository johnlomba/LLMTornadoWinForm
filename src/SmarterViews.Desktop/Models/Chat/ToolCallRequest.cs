using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace SmarterViews.Desktop.Models.Chat;

/// <summary>
/// Represents a pending tool call that requires user approval before execution.
/// This model supports the tool approval workflow including "Always Allow" functionality
/// and remembering user decisions for future tool calls.
/// </summary>
public class ToolCallRequest : INotifyPropertyChanged
{
    private string _toolName = string.Empty;
    private string _arguments = string.Empty;
    private ToolApprovalStatus _status = ToolApprovalStatus.Pending;
    private string? _result;
    private DateTime _requestedAt = DateTime.UtcNow;
    private string? _serverLabel;
    private bool _rememberDecision;
    private bool _alwaysAllow;
    private double? _executionTimeMs;
    
    /// <summary>
    /// Unique identifier for this tool call request.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// The name of the tool being invoked.
    /// </summary>
    public string ToolName
    {
        get => _toolName;
        set => SetField(ref _toolName, value);
    }
    
    /// <summary>
    /// The MCP server label that provides this tool. Null for built-in tools.
    /// </summary>
    public string? ServerLabel
    {
        get => _serverLabel;
        set => SetField(ref _serverLabel, value);
    }
    
    /// <summary>
    /// Raw JSON arguments passed to the tool.
    /// </summary>
    public string Arguments
    {
        get => _arguments;
        set => SetField(ref _arguments, value);
    }
    
    /// <summary>
    /// Returns formatted (pretty-printed) arguments for display purposes.
    /// </summary>
    public string FormattedArguments
    {
        get
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_arguments))
                    return "(no arguments)";
                    
                var parsed = JsonSerializer.Deserialize<JsonElement>(_arguments);
                return JsonSerializer.Serialize(parsed, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return _arguments;
            }
        }
    }
    
    /// <summary>
    /// Current approval status of the tool call request.
    /// </summary>
    public ToolApprovalStatus Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }
    
    /// <summary>
    /// The result returned by the tool after execution, if any.
    /// </summary>
    public string? Result
    {
        get => _result;
        set => SetField(ref _result, value);
    }
    
    /// <summary>
    /// When the tool call was requested.
    /// </summary>
    public DateTime RequestedAt
    {
        get => _requestedAt;
        set => SetField(ref _requestedAt, value);
    }
    
    /// <summary>
    /// Calculates the time elapsed since the request was made.
    /// Useful for timeout handling and UX feedback.
    /// </summary>
    public TimeSpan TimeSinceRequest => DateTime.UtcNow - RequestedAt;
    
    /// <summary>
    /// Whether the user wants to remember their decision for this tool.
    /// When true, future calls to the same tool will use the same approval decision.
    /// </summary>
    public bool RememberDecision
    {
        get => _rememberDecision;
        set => SetField(ref _rememberDecision, value);
    }
    
    /// <summary>
    /// Whether the user has chosen to always allow this tool without prompting.
    /// This is a stronger form of RememberDecision that automatically approves.
    /// </summary>
    public bool AlwaysAllow
    {
        get => _alwaysAllow;
        set => SetField(ref _alwaysAllow, value);
    }
    
    /// <summary>
    /// Execution time in milliseconds, set after the tool completes.
    /// Null if the tool hasn't executed yet or execution failed before timing.
    /// </summary>
    public double? ExecutionTimeMs
    {
        get => _executionTimeMs;
        set => SetField(ref _executionTimeMs, value);
    }
    
    /// <summary>
    /// Returns a formatted string showing execution time (e.g., "1.23s" or "456ms").
    /// Returns null if ExecutionTimeMs is not set.
    /// </summary>
    public string? FormattedExecutionTime
    {
        get
        {
            if (_executionTimeMs == null) return null;
            if (_executionTimeMs >= 1000)
                return $"{_executionTimeMs / 1000:F2}s";
            return $"{_executionTimeMs:F0}ms";
        }
    }
    
    /// <summary>
    /// Task completion source for async approval workflow.
    /// The approval dialog sets the result on this TCS when the user makes a decision.
    /// </summary>
    public TaskCompletionSource<bool>? ApprovalTask { get; set; }
    
    /// <summary>
    /// Creates a display-friendly summary of the tool call for logging or UI.
    /// </summary>
    public string GetDisplaySummary()
    {
        var serverInfo = !string.IsNullOrEmpty(ServerLabel) ? $" [{ServerLabel}]" : "";
        return $"{ToolName}{serverInfo} - {Status}";
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

/// <summary>
/// Status of a tool approval request throughout its lifecycle.
/// </summary>
public enum ToolApprovalStatus
{
    /// <summary>Tool call is waiting for user approval.</summary>
    Pending,
    
    /// <summary>User has approved the tool execution.</summary>
    Approved,
    
    /// <summary>User has denied the tool execution.</summary>
    Denied,
    
    /// <summary>Tool has finished executing successfully.</summary>
    Completed,
    
    /// <summary>An error occurred during tool execution.</summary>
    Error,
    
    /// <summary>Tool was automatically approved based on user's "Always Allow" setting.</summary>
    AutoApproved
}
