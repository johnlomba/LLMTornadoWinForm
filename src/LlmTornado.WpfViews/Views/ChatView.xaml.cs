using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LlmTornado.WpfViews.Models;
using LlmTornado.WpfViews.ViewModels;

namespace LlmTornado.WpfViews.Views;

/// <summary>
/// Interaction logic for ChatView.xaml
/// </summary>
public partial class ChatView : UserControl
{
    public ChatView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }
    
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ChatViewModel oldVm)
        {
            oldVm.ScrollToBottomRequested -= ScrollToBottom;
        }
        
        if (e.NewValue is ChatViewModel newVm)
        {
            newVm.ScrollToBottomRequested += ScrollToBottom;
        }
    }
    
    private void ScrollToBottom()
    {
        Dispatcher.InvokeAsync(() =>
        {
            MessagesScrollViewer.ScrollToEnd();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }
    
    private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (DataContext is ChatViewModel vm && vm.SendMessageCommand.CanExecute(null))
            {
                vm.SendMessageCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
    
    private void ChatView_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var hasValidFiles = files?.Any(f => 
                FileAttachmentModel.IsSupportedExtension(Path.GetExtension(f))) ?? false;
            
            if (hasValidFiles)
            {
                e.Effects = DragDropEffects.Copy;
                DragDropOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        
        e.Handled = true;
    }
    
    private void ChatView_Drop(object sender, DragEventArgs e)
    {
        DragDropOverlay.Visibility = Visibility.Collapsed;
        
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && DataContext is ChatViewModel vm)
            {
                vm.HandleFileDrop(files);
            }
        }
        
        e.Handled = true;
    }
    
    protected override void OnDragLeave(DragEventArgs e)
    {
        base.OnDragLeave(e);
        DragDropOverlay.Visibility = Visibility.Collapsed;
    }
}
