using System.Windows.Controls;
using SmarterViews.Desktop.ViewModels;

namespace SmarterViews.Desktop.Views;

/// <summary>
/// Interaction logic for DataExplorerView.xaml
/// </summary>
public partial class DataExplorerView : UserControl
{
    public DataExplorerView()
    {
        InitializeComponent();
        DataContext = new DataExplorerViewModel();
    }
}
