using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace LlmTornado.WpfViews.Models;

/// <summary>
/// Represents a pending tool call that requires user approval.
/// This class is used by the ToolApprovalDialog to display tool call requests
/// and collect user decisions before tool execution.
/// </summary>
/// <remarks>
/// The approval workflow:
/// 1. ChatService creates a ToolCallRequest when a tool needs execution
/// 2. OnToolApprovalRequired event is raised with this request
/// 3. UI displays the ToolApprovalDialog with the request details
/// 4. User approves or denies the request
/// 5. ApprovalTask is completed with the user's decision
/// 6. ChatService continues or aborts tool execution based on the decision
/// </remarks>
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
    private long _executionTimeMs;
    
    /// <summary>
    /// Unique ID for this request.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Name of the tool being called.
    /// </summary>
    public string ToolName
    {
        get => _toolName;
        set => SetField(ref _toolName, value);
    }
    
    /// <summary>
    /// Arguments passed to the tool (JSON formatted).
    /// </summary>
    public string Arguments
    {
        get => _arguments;
        set => SetField(ref _arguments, value);
    }
    
    /// <summary>
    /// Formatted arguments for display.
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
    /// Current approval status.
    /// </summary>
    public ToolApprovalStatus Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }
    
    /// <summary>
    /// Result of the tool execution (if approved and completed).
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
    /// The MCP server this tool belongs to (if applicable).
    /// </summary>
    public string? ServerLabel
    {
        get => _serverLabel;
        set => SetField(ref _serverLabel, value);
    }
    
    /// <summary>
    /// Whether to remember this decision for future calls to this tool.
    /// </summary>
    public bool RememberDecision
    {
        get => _rememberDecision;
        set => SetField(ref _rememberDecision, value);
    }
    
    /// <summary>
    /// Whether to always allow this tool without prompting.
    /// </summary>
    public bool AlwaysAllow
    {
        get => _alwaysAllow;
        set => SetField(ref _alwaysAllow, value);
    }
    
    /// <summary>
    /// Execution time in milliseconds (if completed).
    /// </summary>
    public long ExecutionTimeMs
    {
        get => _executionTimeMs;
        set => SetField(ref _executionTimeMs, value);
    }
    
    /// <summary>
    /// Human-readable time since request was made.
    /// </summary>
    public string TimeSinceRequest
    {
        get
        {
            var elapsed = DateTime.UtcNow - RequestedAt;
            if (elapsed.TotalSeconds < 60)
                return $"{elapsed.Seconds}s ago";
            if (elapsed.TotalMinutes < 60)
                return $"{elapsed.Minutes}m ago";
            return $"{elapsed.Hours}h ago";
        }
    }
    
    /// <summary>
    /// Event completion source for async approval flow.
    /// </summary>
    public TaskCompletionSource<bool>? ApprovalTask { get; set; }
    
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
/// Status of a tool approval request.
/// </summary>
public enum ToolApprovalStatus
{
    Pending,
    Approved,
    Denied,
    Completed,
    Error
}



