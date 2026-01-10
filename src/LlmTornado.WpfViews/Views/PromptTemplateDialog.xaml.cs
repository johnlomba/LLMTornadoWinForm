using System.Windows;
using System.Windows.Controls;
using LlmTornado.WpfViews.Models;
using LlmTornado.WpfViews.ViewModels;

namespace LlmTornado.WpfViews.Views;

/// <summary>
/// Interaction logic for PromptTemplateDialog.xaml
/// </summary>
public partial class PromptTemplateDialog : UserControl
{
    /// <summary>
    /// Event raised when close is requested.
    /// </summary>
    public event EventHandler? CloseRequested;
    
    /// <summary>
    /// Event raised when a template is selected to use.
    /// </summary>
    public event EventHandler<PromptTemplate>? TemplateSelected;
    
    public PromptTemplateDialog()
    {
        InitializeComponent();
    }
    
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
    
    private void UseSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is PromptTemplateViewModel vm && vm.SelectedTemplate != null)
        {
            TemplateSelected?.Invoke(this, vm.SelectedTemplate);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}

