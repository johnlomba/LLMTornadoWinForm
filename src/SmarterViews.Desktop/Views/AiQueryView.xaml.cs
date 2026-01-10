using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SmarterViews.Desktop.ViewModels;

namespace SmarterViews.Desktop.Views;

/// <summary>
/// Interaction logic for AiQueryView.xaml
/// </summary>
public partial class AiQueryView : UserControl
{
    public AiQueryView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is AiQueryViewModel oldVm)
        {
            oldVm.ScrollToBottomRequested -= ScrollToBottom;
        }

        if (e.NewValue is AiQueryViewModel newVm)
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
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (DataContext is AiQueryViewModel vm && vm.SendMessageCommand.CanExecute(null))
            {
                vm.SendMessageCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
