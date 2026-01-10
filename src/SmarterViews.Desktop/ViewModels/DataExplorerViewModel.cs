using System.Collections.ObjectModel;
using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmarterViews.Desktop.Models;
using SmarterViews.Desktop.Services;
using static SmarterViews.Desktop.Services.SchemaService;

namespace SmarterViews.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Data Explorer view with query builder and result grid
/// Redesigned for intuitive step-by-step query building with schema awareness
/// </summary>
public partial class DataExplorerViewModel : ViewModelBase
{
    private readonly QueryService _queryService;
    private readonly QueryStorageService _queryStorageService;
    private readonly ConnectionStorageService _connectionStorageService;
    private readonly SchemaService _schemaService;

    // Step 1: Database Connection
    [ObservableProperty]
    private ObservableCollection<DatabaseConnection> _availableConnections = [];

    [ObservableProperty]
    private DatabaseConnection? _selectedConnection;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatusText = "No database connected";

    // Step 2: Table Selection
    [ObservableProperty]
    private ObservableCollection<DatabaseObject> _availableTables = [];

    [ObservableProperty]
    private DatabaseObject? _selectedTable;

    [ObservableProperty]
    private bool _hasSelectedTable;

    // Step 3: Columns for filtering
    [ObservableProperty]
    private ObservableCollection<ColumnInfo> _availableColumns = [];

    // Backward compatibility - keep TableName synced with SelectedTable
    [ObservableProperty]
    private string _tableName = string.Empty;

    // Filter condition (AND/OR)
    [ObservableProperty]
    private bool _isAndCondition = true;

    [ObservableProperty]
    private bool _isOrCondition;

    [ObservableProperty]
    private ObservableCollection<QueryRule> _rules = [];

    [ObservableProperty]
    private bool _hasRules;

    [ObservableProperty]
    private ObservableCollection<QueryDefinition> _savedQueries = [];

    [ObservableProperty]
    private QueryDefinition? _selectedQuery;

    [ObservableProperty]
    private string _generatedSql = string.Empty;

    [ObservableProperty]
    private DataTable? _queryResults;

    [ObservableProperty]
    private bool _canExecuteQuery;

    public DataExplorerViewModel() : this(
        new QueryService(), 
        new QueryStorageService(), 
        new ConnectionStorageService(),
        new SchemaService())
    {
    }

    public DataExplorerViewModel(
        QueryService queryService, 
        QueryStorageService queryStorageService,
        ConnectionStorageService connectionStorageService,
        SchemaService schemaService)
    {
        _queryService = queryService;
        _queryStorageService = queryStorageService;
        _connectionStorageService = connectionStorageService;
        _schemaService = schemaService;

        // Load connections on startup
        _ = LoadConnectionsAsync();
    }

    partial void OnSelectedConnectionChanged(DatabaseConnection? value)
    {
        IsConnected = value != null;
        ConnectionStatusText = value != null 
            ? $"Connected to {value.Name}" 
            : "No database connected";
        
        // Clear tables when connection changes
        AvailableTables.Clear();
        SelectedTable = null;
        AvailableColumns.Clear();
        Rules.Clear();
        HasRules = false;
        GeneratedSql = string.Empty;
        QueryResults = null;
        
        // Load tables for the new connection
        if (value != null)
        {
            _ = LoadTablesAsync();
        }
        
        UpdateCanExecuteQuery();
    }

    partial void OnSelectedTableChanged(DatabaseObject? value)
    {
        HasSelectedTable = value != null;
        TableName = value?.DisplayName ?? string.Empty;
        
        // Clear columns and rules when table changes
        AvailableColumns.Clear();
        Rules.Clear();
        HasRules = false;
        GeneratedSql = string.Empty;
        
        // Load columns for the new table
        if (value != null)
        {
            _ = LoadColumnsAsync();
        }
        
        UpdateCanExecuteQuery();
    }

    partial void OnIsAndConditionChanged(bool value)
    {
        if (value)
        {
            IsOrCondition = false;
        }
    }

    partial void OnIsOrConditionChanged(bool value)
    {
        if (value)
        {
            IsAndCondition = false;
        }
    }

    private void UpdateCanExecuteQuery()
    {
        CanExecuteQuery = IsConnected && HasSelectedTable;
    }

    private async Task LoadConnectionsAsync()
    {
        try
        {
            var connections = await _connectionStorageService.LoadAllConnectionsAsync();
            AvailableConnections.Clear();
            foreach (var conn in connections)
            {
                AvailableConnections.Add(conn);
            }
            
            // Auto-select default connection if available
            var defaultConnection = connections.FirstOrDefault(c => c.IsDefault) ?? connections.FirstOrDefault();
            if (defaultConnection != null)
            {
                SelectedConnection = defaultConnection;
            }
        }
        catch (Exception ex)
        {
            SetError($"Failed to load connections: {ex.Message}");
        }
    }

    private async Task LoadTablesAsync()
    {
        if (SelectedConnection == null) return;

        IsLoading = true;
        ClearError();

        try
        {
            var tables = await _schemaService.GetTablesAndViewsAsync(SelectedConnection);
            AvailableTables.Clear();
            foreach (var table in tables.OrderBy(t => t.DisplayName))
            {
                AvailableTables.Add(table);
            }
        }
        catch (Exception ex)
        {
            SetError($"Failed to load tables: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadColumnsAsync()
    {
        if (SelectedConnection == null || SelectedTable == null) return;

        try
        {
            var columns = await _schemaService.GetColumnsAsync(
                SelectedConnection, 
                SelectedTable.Name, 
                string.IsNullOrEmpty(SelectedTable.Schema) ? null : SelectedTable.Schema);
            
            AvailableColumns.Clear();
            foreach (var column in columns)
            {
                AvailableColumns.Add(column);
            }
        }
        catch (Exception ex)
        {
            SetError($"Failed to load columns: {ex.Message}");
        }
    }

    [RelayCommand]
    private void AddRule()
    {
        var rule = new QueryRule();
        
        // Pre-select first column if available
        if (AvailableColumns.Count > 0)
        {
            rule.SelectedColumn = AvailableColumns[0];
        }
        
        Rules.Add(rule);
        HasRules = Rules.Count > 0;
    }

    [RelayCommand]
    private void RemoveRule(QueryRule? rule)
    {
        if (rule != null)
        {
            Rules.Remove(rule);
            HasRules = Rules.Count > 0;
        }
    }

    [RelayCommand]
    private void ClearRules()
    {
        Rules.Clear();
        HasRules = false;
    }

    [RelayCommand]
    private void GenerateQuery()
    {
        if (!HasSelectedTable || SelectedTable == null)
        {
            SetError("Please select a table first");
            return;
        }

        ClearError();

        try
        {
            var condition = IsAndCondition ? "AND" : "OR";
            var ruleSet = new RuleSet
            {
                Condition = condition,
                Rules = Rules.Select(r => new QueryRule
                {
                    Field = r.Field,
                    Operator = r.Operator,
                    Value = r.GetEffectiveValue()
                }).ToList()
            };

            var query = _queryService.CreateQuery(SelectedTable.DisplayName);
            query = _queryService.ApplyRules(query, ruleSet);
            var result = _queryService.Compile(query);
            GeneratedSql = result.Sql;
        }
        catch (Exception ex)
        {
            SetError($"Failed to generate query: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ExecuteQueryAsync()
    {
        if (SelectedConnection == null)
        {
            SetError("Please select a database connection");
            return;
        }

        if (!HasSelectedTable || SelectedTable == null)
        {
            SetError("Please select a table first");
            return;
        }

        IsLoading = true;
        ClearError();

        try
        {
            // Generate SQL if not already generated
            if (string.IsNullOrWhiteSpace(GeneratedSql))
            {
                GenerateQuery();
            }

            if (string.IsNullOrWhiteSpace(GeneratedSql))
            {
                SetError("Failed to generate SQL query");
                return;
            }

            QueryResults = await _schemaService.ExecuteQueryAsync(SelectedConnection, GeneratedSql);
        }
        catch (Exception ex)
        {
            SetError($"Query failed: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveQueryAsync()
    {
        if (!HasSelectedTable || SelectedTable == null)
        {
            SetError("Please select a table before saving");
            return;
        }

        ClearError();

        try
        {
            var condition = IsAndCondition ? "AND" : "OR";
            var queryDef = new QueryDefinition
            {
                Name = $"Query_{SelectedTable.Name}_{DateTime.Now:yyyyMMdd_HHmmss}",
                Table = SelectedTable.DisplayName,
                Rules = new RuleSet
                {
                    Condition = condition,
                    Rules = Rules.Select(r => new QueryRule
                    {
                        Field = r.Field,
                        Operator = r.Operator,
                        Value = r.GetEffectiveValue()
                    }).ToList()
                }
            };

            await _queryStorageService.SaveQueryAsync(queryDef);
            SavedQueries.Add(queryDef);
        }
        catch (Exception ex)
        {
            SetError($"Failed to save query: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task LoadSavedQueriesAsync()
    {
        IsLoading = true;
        ClearError();

        try
        {
            var queries = await _queryStorageService.LoadAllQueriesAsync();
            SavedQueries.Clear();
            foreach (var query in queries)
            {
                SavedQueries.Add(query);
            }
        }
        catch (Exception ex)
        {
            SetError($"Failed to load saved queries: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void LoadSelectedQuery()
    {
        if (SelectedQuery == null)
        {
            return;
        }

        // Find matching table
        var matchingTable = AvailableTables.FirstOrDefault(t => 
            t.DisplayName.Equals(SelectedQuery.Table, StringComparison.OrdinalIgnoreCase) ||
            t.Name.Equals(SelectedQuery.Table, StringComparison.OrdinalIgnoreCase));
        
        if (matchingTable != null)
        {
            SelectedTable = matchingTable;
        }
        else
        {
            TableName = SelectedQuery.Table;
        }

        // Set condition
        IsAndCondition = SelectedQuery.Rules?.Condition != "OR";
        IsOrCondition = SelectedQuery.Rules?.Condition == "OR";

        // Load rules
        Rules.Clear();
        if (SelectedQuery.Rules?.Rules != null)
        {
            foreach (var savedRule in SelectedQuery.Rules.Rules)
            {
                var rule = new QueryRule
                {
                    Field = savedRule.Field,
                    Operator = savedRule.Operator
                };

                // Find matching column
                var matchingColumn = AvailableColumns.FirstOrDefault(c => 
                    c.Name.Equals(savedRule.Field, StringComparison.OrdinalIgnoreCase));
                
                if (matchingColumn != null)
                {
                    rule.SelectedColumn = matchingColumn;
                }

                // Set value based on type
                if (savedRule.Value != null)
                {
                    rule.StringValue = savedRule.Value.ToString() ?? string.Empty;
                }

                // Find matching operator
                var matchingOperator = rule.AvailableOperators.FirstOrDefault(o => 
                    o.Value.Equals(savedRule.Operator, StringComparison.OrdinalIgnoreCase));
                
                if (matchingOperator != null)
                {
                    rule.SelectedOperator = matchingOperator;
                }

                Rules.Add(rule);
            }
        }

        HasRules = Rules.Count > 0;
        GenerateQuery();
    }

    /// <summary>
    /// Refreshes the list of available connections
    /// </summary>
    [RelayCommand]
    private async Task RefreshConnectionsAsync()
    {
        await LoadConnectionsAsync();
    }

    /// <summary>
    /// Imports a query from a JSON file
    /// </summary>
    [RelayCommand]
    private async Task ImportQueryAsync()
    {
        ClearError();

        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import Query",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                int importedCount = 0;
                foreach (var filePath in dialog.FileNames)
                {
                    try
                    {
                        var json = await System.IO.File.ReadAllTextAsync(filePath);
                        var query = System.Text.Json.JsonSerializer.Deserialize<QueryDefinition>(json);
                        
                        if (query != null)
                        {
                            // Save to the app's query storage
                            await _queryStorageService.SaveQueryAsync(query);
                            SavedQueries.Add(query);
                            importedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        SetError($"Failed to import {System.IO.Path.GetFileName(filePath)}: {ex.Message}");
                    }
                }

                if (importedCount > 0 && string.IsNullOrEmpty(ErrorMessage))
                {
                    // Success - no error message needed, the queries appear in the list
                }
            }
        }
        catch (Exception ex)
        {
            SetError($"Import failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Exports the selected query to a JSON file
    /// </summary>
    [RelayCommand]
    private async Task ExportQueryAsync()
    {
        if (SelectedQuery == null)
        {
            SetError("Please select a query to export");
            return;
        }

        ClearError();

        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Query",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                FileName = $"{SelectedQuery.Name}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(SelectedQuery, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                await System.IO.File.WriteAllTextAsync(dialog.FileName, json);
            }
        }
        catch (Exception ex)
        {
            SetError($"Export failed: {ex.Message}");
        }
    }
}
