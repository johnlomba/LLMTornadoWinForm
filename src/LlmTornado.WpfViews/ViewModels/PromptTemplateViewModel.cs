using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LlmTornado.WpfViews.Models;
using LlmTornado.WpfViews.Services;

namespace LlmTornado.WpfViews.ViewModels;

/// <summary>
/// ViewModel for the prompt template editor dialog.
/// </summary>
public partial class PromptTemplateViewModel : ObservableObject
{
    private readonly PromptTemplateService _templateService;
    
    [ObservableProperty]
    private PromptTemplate? _selectedTemplate;
    
    [ObservableProperty]
    private string _editName = string.Empty;
    
    [ObservableProperty]
    private string _editDescription = string.Empty;
    
    [ObservableProperty]
    private string _editContent = string.Empty;
    
    [ObservableProperty]
    private bool _isNewTemplate;
    
    [ObservableProperty]
    private bool _hasChanges;
    
    /// <summary>
    /// Collection of all templates.
    /// </summary>
    public ObservableCollection<PromptTemplate> Templates { get; } = [];
    
    /// <summary>
    /// Event raised when templates are updated.
    /// </summary>
    public event Action? TemplatesUpdated;
    
    public PromptTemplateViewModel(PromptTemplateService templateService)
    {
        _templateService = templateService;
    }
    
    /// <summary>
    /// Loads all templates.
    /// </summary>
    public async Task LoadTemplatesAsync()
    {
        var templates = await _templateService.GetAllTemplatesAsync();
        Templates.Clear();
        foreach (var template in templates)
        {
            Templates.Add(template);
        }
        
        // Select first template if none selected
        if (SelectedTemplate == null && Templates.Count > 0)
        {
            SelectedTemplate = Templates[0];
        }
    }
    
    /// <summary>
    /// Creates a new template.
    /// </summary>
    [RelayCommand]
    public void NewTemplate()
    {
        var newTemplate = new PromptTemplate
        {
            Id = Guid.NewGuid().ToString(),
            Name = "New Template",
            Content = "You are a helpful assistant.",
            IsBuiltIn = false
        };
        
        Templates.Add(newTemplate);
        SelectedTemplate = newTemplate;
        IsNewTemplate = true;
        HasChanges = true;
    }
    
    /// <summary>
    /// Saves the current template.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSaveTemplate))]
    public async Task SaveTemplateAsync()
    {
        if (SelectedTemplate == null || SelectedTemplate.IsBuiltIn)
            return;
        
        // Update the template with edited values
        SelectedTemplate.Name = EditName;
        SelectedTemplate.Description = string.IsNullOrWhiteSpace(EditDescription) ? null : EditDescription;
        SelectedTemplate.Content = EditContent;
        
        if (IsNewTemplate)
        {
            await _templateService.AddTemplateAsync(SelectedTemplate);
            IsNewTemplate = false;
        }
        else
        {
            await _templateService.UpdateTemplateAsync(SelectedTemplate);
        }
        
        HasChanges = false;
        TemplatesUpdated?.Invoke();
    }
    
    private bool CanSaveTemplate()
    {
        return SelectedTemplate != null 
            && !SelectedTemplate.IsBuiltIn 
            && !string.IsNullOrWhiteSpace(EditName)
            && !string.IsNullOrWhiteSpace(EditContent);
    }
    
    /// <summary>
    /// Deletes the current template.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteTemplate))]
    public async Task DeleteTemplateAsync()
    {
        if (SelectedTemplate == null || SelectedTemplate.IsBuiltIn)
            return;
        
        await _templateService.DeleteTemplateAsync(SelectedTemplate.Id);
        Templates.Remove(SelectedTemplate);
        
        // Select another template
        SelectedTemplate = Templates.FirstOrDefault();
        IsNewTemplate = false;
        HasChanges = false;
        TemplatesUpdated?.Invoke();
    }
    
    private bool CanDeleteTemplate()
    {
        return SelectedTemplate != null && !SelectedTemplate.IsBuiltIn && !IsNewTemplate;
    }
    
    /// <summary>
    /// Called when the selected template changes.
    /// </summary>
    partial void OnSelectedTemplateChanged(PromptTemplate? value)
    {
        if (value != null)
        {
            EditName = value.Name;
            EditDescription = value.Description ?? string.Empty;
            EditContent = value.Content;
            IsNewTemplate = false;
        }
        else
        {
            EditName = string.Empty;
            EditDescription = string.Empty;
            EditContent = string.Empty;
        }
        
        HasChanges = false;
        SaveTemplateCommand.NotifyCanExecuteChanged();
        DeleteTemplateCommand.NotifyCanExecuteChanged();
    }
    
    partial void OnEditNameChanged(string value)
    {
        HasChanges = true;
        SaveTemplateCommand.NotifyCanExecuteChanged();
    }
    
    partial void OnEditDescriptionChanged(string value)
    {
        HasChanges = true;
    }
    
    partial void OnEditContentChanged(string value)
    {
        HasChanges = true;
        SaveTemplateCommand.NotifyCanExecuteChanged();
    }
}



