using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LlmTornado.WpfViews.Models;

namespace LlmTornado.WpfViews.ViewModels;

/// <summary>
/// ViewModel for the tool approval dialog.
/// </summary>
public partial class ToolApprovalViewModel : ObservableObject
{
    [ObservableProperty]
    private ToolCallRequest? _currentRequest;
    
    [ObservableProperty]
    private bool _isVisible;
    
    [ObservableProperty]
    private bool _requireApproval = true;
    
    /// <summary>
    /// History of tool calls in this session.
    /// </summary>
    public ObservableCollection<ToolCallRequest> ToolCallHistory { get; } = [];
    
    /// <summary>
    /// Event raised when the dialog should be closed with approval result.
    /// </summary>
    public event Action<bool>? ApprovalCompleted;
    
    /// <summary>
    /// Shows a tool call request for approval.
    /// </summary>
    public void ShowRequest(ToolCallRequest request)
    {
        CurrentRequest = request;
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
            
            // Complete the TaskCompletionSource to allow tool execution
            CurrentRequest.ApprovalTask?.TrySetResult(true);
        }
        
        IsVisible = false;
        ApprovalCompleted?.Invoke(true);
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
            
            // Complete the TaskCompletionSource to deny tool execution
            CurrentRequest.ApprovalTask?.TrySetResult(false);
        }
        
        IsVisible = false;
        ApprovalCompleted?.Invoke(false);
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
    public void UpdateToolResult(string toolName, string result)
    {
        var request = ToolCallHistory.FirstOrDefault(r => 
            r.ToolName == toolName && r.Status == ToolApprovalStatus.Approved);
        
        if (request != null)
        {
            request.Status = ToolApprovalStatus.Completed;
            request.Result = result;
        }
    }
}

