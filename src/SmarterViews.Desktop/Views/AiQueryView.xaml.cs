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
    private AiQueryViewModel? _viewModel;
    
    public AiQueryView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        // Initialize ViewModel if not already set
        if (DataContext == null)
        {
            _viewModel = new AiQueryViewModel();
            DataContext = _viewModel;
            _ = _viewModel.InitializeAsync();
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is AiQueryViewModel oldVm)
        {
            oldVm.ScrollToBottomRequested -= ScrollToBottom;
        }

        if (e.NewValue is AiQueryViewModel newVm)
        {
            _viewModel = newVm;
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
            if (_viewModel != null && _viewModel.SendMessageCommand.CanExecute(null))
            {
                _viewModel.SendMessageCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void McpButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null && _viewModel.OpenMcpServersCommand.CanExecute(null))
        {
            _viewModel.OpenMcpServersCommand.Execute(null);
        }
    }

    private void AiSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null && _viewModel.OpenSettingsCommand.CanExecute(null))
        {
            _viewModel.OpenSettingsCommand.Execute(null);
        }
    }

    private void ExecuteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null && _viewModel.ExecuteQueryCommand.CanExecute(null))
        {
            _viewModel.ExecuteQueryCommand.Execute(null);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null && _viewModel.SaveQueryCommand.CanExecute(null))
        {
            _viewModel.SaveQueryCommand.Execute(null);
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null && _viewModel.CopySqlCommand.CanExecute(null))
        {
            _viewModel.CopySqlCommand.Execute(null);
        }
    }
}
