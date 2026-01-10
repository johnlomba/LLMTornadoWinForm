using System.Windows;

namespace SmarterViews.Desktop;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void DatabaseConnections_Click(object sender, RoutedEventArgs e)
    {
        var connectionsWindow = new ConnectionsWindow { Owner = this };
        connectionsWindow.ShowDialog();
    }
}