using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LlmTornado.WpfViews.Services;
using LlmTornado.WpfViews.ViewModels;

namespace LlmTornado.WpfViews.Views;

/// <summary>
/// Interaction logic for ConversationList.xaml
/// </summary>
public partial class ConversationList : UserControl
{
    public ConversationList()
    {
        InitializeComponent();
    }
    
    private async void ConversationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && 
            listBox.SelectedItem is ConversationSummary conversation &&
            DataContext is MainViewModel vm)
        {
            await vm.SelectConversationCommand.ExecuteAsync(conversation);
        }
    }
}

