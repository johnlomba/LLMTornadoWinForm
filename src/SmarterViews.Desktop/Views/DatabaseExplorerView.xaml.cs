using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SmarterViews.Desktop.ViewModels;

namespace SmarterViews.Desktop.Views;

/// <summary>
/// Code-behind for DatabaseExplorerView.xaml
/// </summary>
public partial class DatabaseExplorerView : UserControl
{
    private DatabaseExplorerViewModel ViewModel => (DatabaseExplorerViewModel)DataContext;

    public DatabaseExplorerView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        // Load connections when the view loads
        await ViewModel.LoadConnectionsAsync();
    }

    private void SchemaTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is SchemaTreeItem item)
        {
            ViewModel.SelectedSchemaItem = item;
        }
    }

    private async void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem treeViewItem && 
            treeViewItem.DataContext is SchemaTreeItem schemaItem)
        {
            // Load columns when a table/view is expanded
            if (schemaItem.ItemType == SchemaItemType.Table || schemaItem.ItemType == SchemaItemType.View)
            {
                await ViewModel.LoadColumnsAsync(schemaItem);
            }
        }
    }

    private async void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem treeViewItem && 
            treeViewItem.DataContext is SchemaTreeItem schemaItem)
        {
            // Preview table/view on double-click
            if (schemaItem.ItemType == SchemaItemType.Table || schemaItem.ItemType == SchemaItemType.View)
            {
                e.Handled = true;
                await ViewModel.PreviewTableAsync(schemaItem);
            }
        }
    }
}
