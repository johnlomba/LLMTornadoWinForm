using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmarterViews.Desktop.Models;
using SmarterViews.Desktop.Services;

namespace SmarterViews.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Database Explorer view with schema browser and SQL editor
/// </summary>
public partial class DatabaseExplorerViewModel : ViewModelBase
{
    private readonly ConnectionStorageService _connectionStorageService;
    private readonly SchemaService _schemaService;

    [ObservableProperty]
    private ObservableCollection<DatabaseConnection> _connections = new();

    [ObservableProperty]
    private DatabaseConnection? _selectedConnection;

    [ObservableProperty]
    private ObservableCollection<SchemaTreeItem> _schemaTree = new();

    [ObservableProperty]
    private SchemaTreeItem? _selectedSchemaItem;

    [ObservableProperty]
    private string _sqlText = string.Empty;

    [ObservableProperty]
    private DataTable? _queryResults;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private int _rowCount;

    [ObservableProperty]
    private string _executionTime = string.Empty;

    [ObservableProperty]
    private bool _isExecuting;

    public DatabaseExplorerViewModel() : this(new ConnectionStorageService(), new SchemaService())
    {
    }

    public DatabaseExplorerViewModel(ConnectionStorageService connectionStorageService, SchemaService schemaService)
    {
        _connectionStorageService = connectionStorageService;
        _schemaService = schemaService;
    }

    /// <summary>
    /// Loads all saved database connections
    /// </summary>
    [RelayCommand]
    public async Task LoadConnectionsAsync()
    {
        try
        {
            IsLoading = true;
            ClearError();

            var connections = await _connectionStorageService.LoadAllConnectionsAsync();
            Connections.Clear();
            foreach (var conn in connections)
            {
                Connections.Add(conn);
            }

            // Auto-select default connection
            var defaultConnection = Connections.FirstOrDefault(c => c.IsDefault) ?? Connections.FirstOrDefault();
            if (defaultConnection != null)
            {
                SelectedConnection = defaultConnection;
            }
        }
        catch (Exception ex)
        {
            SetError($"Failed to load connections: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Called when the selected connection changes - loads the schema
    /// </summary>
    partial void OnSelectedConnectionChanged(DatabaseConnection? value)
    {
        if (value != null)
        {
            _ = LoadSchemaAsync();
        }
        else
        {
            SchemaTree.Clear();
        }
    }

    /// <summary>
    /// Loads the database schema (tables and views)
    /// </summary>
    [RelayCommand]
    public async Task LoadSchemaAsync()
    {
        if (SelectedConnection == null)
        {
            SetError("Please select a database connection");
            return;
        }

        try
        {
            IsLoading = true;
            ClearError();
            StatusMessage = "Loading schema...";
            SchemaTree.Clear();

            var objects = await _schemaService.GetTablesAndViewsAsync(SelectedConnection);

            // Group by type (Tables and Views)
            var tables = objects.Where(o => o.Type == "TABLE").ToList();
            var views = objects.Where(o => o.Type == "VIEW").ToList();

            // Add Tables folder
            if (tables.Any())
            {
                var tablesFolder = new SchemaTreeItem
                {
                    Name = "Tables",
                    ItemType = SchemaItemType.Folder,
                    Icon = "📁"
                };

                foreach (var table in tables)
                {
                    var tableItem = new SchemaTreeItem
                    {
                        Name = table.Name,
                        Schema = table.Schema,
                        ItemType = SchemaItemType.Table,
                        Icon = "📋",
                        DatabaseObject = table
                    };
                    tablesFolder.Children.Add(tableItem);
                }

                SchemaTree.Add(tablesFolder);
            }

            // Add Views folder
            if (views.Any())
            {
                var viewsFolder = new SchemaTreeItem
                {
                    Name = "Views",
                    ItemType = SchemaItemType.Folder,
                    Icon = "📁"
                };

                foreach (var view in views)
                {
                    var viewItem = new SchemaTreeItem
                    {
                        Name = view.Name,
                        Schema = view.Schema,
                        ItemType = SchemaItemType.View,
                        Icon = "👁",
                        DatabaseObject = view
                    };
                    viewsFolder.Children.Add(viewItem);
                }

                SchemaTree.Add(viewsFolder);
            }

            StatusMessage = $"Loaded {tables.Count} tables, {views.Count} views";
        }
        catch (Exception ex)
        {
            SetError($"Failed to load schema: {ex.Message}");
            StatusMessage = "Error loading schema";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads columns for a selected table or view
    /// </summary>
    [RelayCommand]
    public async Task LoadColumnsAsync(SchemaTreeItem item)
    {
        if (SelectedConnection == null || item.DatabaseObject == null)
            return;

        if (item.Children.Any())
            return; // Already loaded

        try
        {
            var columns = await _schemaService.GetColumnsAsync(
                SelectedConnection,
                item.DatabaseObject.Name,
                item.DatabaseObject.Schema);

            foreach (var column in columns)
            {
                item.Children.Add(new SchemaTreeItem
                {
                    Name = column.Name,
                    ItemType = SchemaItemType.Column,
                    Icon = column.IsPrimaryKey ? "🔑" : "📊",
                    DataType = column.DataType
                });
            }
        }
        catch (Exception ex)
        {
            SetError($"Failed to load columns: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a SELECT query for the selected table/view
    /// </summary>
    [RelayCommand]
    public void GenerateSelectQuery(SchemaTreeItem? item = null)
    {
        var targetItem = item ?? SelectedSchemaItem;
        
        if (SelectedConnection == null || targetItem?.DatabaseObject == null)
            return;

        SqlText = _schemaService.GenerateSelectQuery(
            targetItem.DatabaseObject.Name,
            targetItem.DatabaseObject.Schema,
            SelectedConnection.DatabaseType);
    }

    /// <summary>
    /// Executes the SQL query in the editor
    /// </summary>
    [RelayCommand]
    public async Task ExecuteQueryAsync()
    {
        if (SelectedConnection == null)
        {
            SetError("Please select a database connection");
            return;
        }

        if (string.IsNullOrWhiteSpace(SqlText))
        {
            SetError("Please enter a SQL query");
            return;
        }

        try
        {
            IsExecuting = true;
            ClearError();
            StatusMessage = "Executing query...";
            QueryResults = null;

            var stopwatch = Stopwatch.StartNew();

            // Determine if it's a SELECT query
            var trimmedSql = SqlText.Trim().ToUpperInvariant();
            if (trimmedSql.StartsWith("SELECT") || trimmedSql.StartsWith("WITH"))
            {
                var results = await _schemaService.ExecuteQueryAsync(SelectedConnection, SqlText);
                stopwatch.Stop();

                QueryResults = results;
                RowCount = results.Rows.Count;
                ExecutionTime = $"{stopwatch.ElapsedMilliseconds}ms";
                StatusMessage = $"Query completed - {RowCount} row(s) returned in {ExecutionTime}";
            }
            else
            {
                // Non-SELECT queries (INSERT, UPDATE, DELETE, etc.)
                var affectedRows = await _schemaService.ExecuteNonQueryAsync(SelectedConnection, SqlText);
                stopwatch.Stop();

                ExecutionTime = $"{stopwatch.ElapsedMilliseconds}ms";
                RowCount = affectedRows;
                StatusMessage = $"Query completed - {affectedRows} row(s) affected in {ExecutionTime}";
            }
        }
        catch (Exception ex)
        {
            SetError($"Query execution failed: {ex.Message}");
            StatusMessage = "Query failed";
        }
        finally
        {
            IsExecuting = false;
        }
    }

    /// <summary>
    /// Clears the current query and results
    /// </summary>
    [RelayCommand]
    public void ClearQuery()
    {
        SqlText = string.Empty;
        QueryResults = null;
        RowCount = 0;
        ExecutionTime = string.Empty;
        StatusMessage = "Ready";
        ClearError();
    }

    /// <summary>
    /// Selects a table/view and executes a preview query
    /// </summary>
    [RelayCommand]
    public async Task PreviewTableAsync(SchemaTreeItem item)
    {
        if (item.ItemType != SchemaItemType.Table && item.ItemType != SchemaItemType.View)
            return;

        GenerateSelectQuery(item);
        await ExecuteQueryAsync();
    }
}

/// <summary>
/// Types of items in the schema tree
/// </summary>
public enum SchemaItemType
{
    Folder,
    Table,
    View,
    Column
}

/// <summary>
/// Represents an item in the schema tree view
/// </summary>
public partial class SchemaTreeItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _schema;

    [ObservableProperty]
    private SchemaItemType _itemType;

    [ObservableProperty]
    private string _icon = string.Empty;

    [ObservableProperty]
    private string? _dataType;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private ObservableCollection<SchemaTreeItem> _children = new();

    public SchemaService.DatabaseObject? DatabaseObject { get; set; }

    public string DisplayName => string.IsNullOrEmpty(DataType) ? Name : $"{Name} ({DataType})";
}
