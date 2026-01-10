using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LlmTornado.WpfViews.Models;

namespace LlmTornado.WpfViews.ViewModels;

/// <summary>
/// ViewModel for the tool approval dialog.
/// Manages tool call approval workflow, including remembering decisions 
/// and displaying tool call history.
/// </summary>
/// <remarks>
/// This ViewModel supports:
/// - Single tool approval/denial
/// - "Always allow" option to skip future prompts for a specific tool
/// - Tool call history tracking with results
/// - Configurable default behavior
/// </remarks>
public partial class ToolApprovalViewModel : ObservableObject
{
    private readonly HashSet<string> _alwaysAllowedTools = [];
    private readonly HashSet<string> _alwaysDeniedTools = [];
    
    [ObservableProperty]
    private ToolCallRequest? _currentRequest;
    
    [ObservableProperty]
    private bool _isVisible;
    
    [ObservableProperty]
    private bool _requireApproval = true;
    
    [ObservableProperty]
    private bool _rememberDecision;
    
    /// <summary>
    /// History of tool calls in this session.
    /// </summary>
    public ObservableCollection<ToolCallRequest> ToolCallHistory { get; } = [];
    
    /// <summary>
    /// List of tools that are always allowed without prompting.
    /// </summary>
    public ObservableCollection<string> AlwaysAllowedTools { get; } = [];
    
    /// <summary>
    /// Event raised when the dialog should be closed with approval result.
    /// </summary>
    public event Action<bool>? ApprovalCompleted;
    
    /// <summary>
    /// Checks if a tool should be auto-approved based on previous decisions.
    /// </summary>
    /// <param name="toolName">The name of the tool to check.</param>
    /// <returns>True if auto-approved, false if auto-denied, null if user should be prompted.</returns>
    public bool? ShouldAutoApprove(string toolName)
    {
        if (!RequireApproval) return true;
        if (_alwaysAllowedTools.Contains(toolName)) return true;
        if (_alwaysDeniedTools.Contains(toolName)) return false;
        return null;
    }
    
    /// <summary>
    /// Shows a tool call request for approval.
    /// </summary>
    public void ShowRequest(ToolCallRequest request)
    {
        CurrentRequest = request;
        RememberDecision = false;
        ToolCallHistory.Insert(0, request);
        IsVisible = true;
    }
    
    /// <summary>
    /// Approves the current tool call.
    /// </summary>
    [RelayCommand]
    public void Approve()
    {
        if (CurrentRequest != null)
        {
            CurrentRequest.Status = ToolApprovalStatus.Approved;
            
            // Handle "remember decision" option
            if (RememberDecision)
            {
                _alwaysAllowedTools.Add(CurrentRequest.ToolName);
                if (!AlwaysAllowedTools.Contains(CurrentRequest.ToolName))
                {
                    AlwaysAllowedTools.Add(CurrentRequest.ToolName);
                }
                CurrentRequest.AlwaysAllow = true;
            }
            
            // Complete the TaskCompletionSource to allow tool execution
            CurrentRequest.ApprovalTask?.TrySetResult(true);
        }
        
        IsVisible = false;
        RememberDecision = false;
        ApprovalCompleted?.Invoke(true);
    }
    
    /// <summary>
    /// Approves and sets this tool to always be allowed without prompting.
    /// </summary>
    [RelayCommand]
    public void ApproveAlways()
    {
        RememberDecision = true;
        Approve();
    }
    
    /// <summary>
    /// Denies the current tool call.
    /// </summary>
    [RelayCommand]
    public void Deny()
    {
        if (CurrentRequest != null)
        {
            CurrentRequest.Status = ToolApprovalStatus.Denied;
            
            // Handle "remember decision" option
            if (RememberDecision)
            {
                _alwaysDeniedTools.Add(CurrentRequest.ToolName);
                CurrentRequest.RememberDecision = true;
            }
            
            // Complete the TaskCompletionSource to deny tool execution
            CurrentRequest.ApprovalTask?.TrySetResult(false);
        }
        
        IsVisible = false;
        RememberDecision = false;
        ApprovalCompleted?.Invoke(false);
    }
    
    /// <summary>
    /// Removes a tool from the always-allowed list.
    /// </summary>
    [RelayCommand]
    public void RemoveAlwaysAllowed(string? toolName)
    {
        if (!string.IsNullOrEmpty(toolName))
        {
            _alwaysAllowedTools.Remove(toolName);
            AlwaysAllowedTools.Remove(toolName);
        }
    }
    
    /// <summary>
    /// Clears all auto-approval settings.
    /// </summary>
    [RelayCommand]
    public void ClearAutoApprovals()
    {
        _alwaysAllowedTools.Clear();
        _alwaysDeniedTools.Clear();
        AlwaysAllowedTools.Clear();
    }
    
    /// <summary>
    /// Clears the tool call history.
    /// </summary>
    [RelayCommand]
    public void ClearHistory()
    {
        ToolCallHistory.Clear();
    }
    
    /// <summary>
    /// Updates the status and result of a tool call.
    /// </summary>
    /// <param name="toolName">Name of the completed tool.</param>
    /// <param name="result">Result of the tool execution.</param>
    /// <param name="executionTimeMs">Execution time in milliseconds.</param>
    public void UpdateToolResult(string toolName, string result, long executionTimeMs = 0)
    {
        var request = ToolCallHistory.FirstOrDefault(r => 
            r.ToolName == toolName && r.Status == ToolApprovalStatus.Approved);
        
        if (request != null)
        {
            request.Status = ToolApprovalStatus.Completed;
            request.Result = result;
            request.ExecutionTimeMs = executionTimeMs;
        }
    }
}

