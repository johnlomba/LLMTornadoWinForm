using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmarterViews.Desktop.Models;
using SmarterViews.Desktop.Models.Chat;
using SmarterViews.Desktop.Services;
using SmarterViews.Desktop.Services.Chat;
using SmarterViews.Desktop.ViewModels.Chat;
using static SmarterViews.Desktop.Services.SchemaService;

namespace SmarterViews.Desktop.ViewModels;

/// <summary>
/// ViewModel for the AI Query view that combines database context selection with AI-powered query generation.
/// Includes MCP support, prompt templates, conversation history, and SQL saving.
/// </summary>
public partial class AiQueryViewModel : ViewModelBase
{
    private readonly ConnectionStorageService _connectionStorageService;
    private readonly SchemaService _schemaService;
    private readonly ChatService _chatService;
    private readonly ConversationStore _conversationStore;
    private readonly McpServerManager _mcpServerManager;
    private readonly PromptTemplateService _promptTemplateService;
    private readonly SavedSqlQueryService _savedSqlQueryService;
    private readonly Stopwatch _requestStopwatch = new();
    private MessageViewModel? _currentStreamingMessage;
    
    /// <summary>
    /// Tracks tools that the user has chosen to always allow without prompting.
    /// Key format: "toolName" or "serverLabel:toolName" for MCP tools.
    /// </summary>
    private readonly HashSet<string> _alwaysAllowedTools = new();
    
    /// <summary>
    /// Tracks tools that the user has chosen to always deny without prompting.
    /// Key format: "toolName" or "serverLabel:toolName" for MCP tools.
    /// </summary>
    private readonly HashSet<string> _alwaysDeniedTools = new();

    // Database Context
    [ObservableProperty]
    private ObservableCollection<DatabaseConnection> _availableConnections = [];

    [ObservableProperty]
    private DatabaseConnection? _selectedConnection;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatusText = "No database connected";

    [ObservableProperty]
    private ObservableCollection<DatabaseObject> _availableTables = [];

    [ObservableProperty]
    private DatabaseObject? _selectedTable;

    [ObservableProperty]
    private ObservableCollection<ColumnInfo> _availableColumns = [];

    [ObservableProperty]
    private string _schemaPreview = "Select a database to see schema...";

    // LLM Configuration
    [ObservableProperty]
    private ObservableCollection<ModelOption> _availableModels = [];

    [ObservableProperty]
    private ModelOption? _selectedModel;

    [ObservableProperty]
    private bool _isApiKeyConfigured;

    [ObservableProperty]
    private string _apiKeyStatusText = "Not configured";

    private SettingsModel _settings = new();

    // Prompt Templates
    [ObservableProperty]
    private ObservableCollection<PromptTemplate> _availableTemplates = [];

    [ObservableProperty]
    private PromptTemplate? _selectedTemplate;

    // Saved SQL Queries
    [ObservableProperty]
    private ObservableCollection<SavedSqlQuery> _savedQueries = [];

    [ObservableProperty]
    private SavedSqlQuery? _selectedSavedQuery;
    
    [ObservableProperty]
    private string _queryName = string.Empty;

    // Conversation History
    [ObservableProperty]
    private ObservableCollection<ConversationModel> _conversationHistory = [];

    [ObservableProperty]
    private ConversationModel? _selectedConversation;

    // MCP Server Status
    [ObservableProperty]
    private int _connectedMcpServers;

    [ObservableProperty]
    private int _availableMcpTools;

    [ObservableProperty]
    private bool _isMcpEnabled;

    // Tool Approval
    [ObservableProperty]
    private ToolCallRequest? _pendingToolApproval;

    [ObservableProperty]
    private bool _isToolApprovalVisible = false;

    // Chat State
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    private string _inputText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _isChatInitialized;

    public ObservableCollection<MessageViewModel> Messages { get; } = [];

    public bool HasMessages => Messages.Count > 0;

    // Query State
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteQueryCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopySqlCommand))]
    private string _generatedSql = string.Empty;

    [ObservableProperty]
    private DataTable? _queryResults;

    [ObservableProperty]
    private string _resultStatusText = "Ready - ask a question to generate SQL";

    [ObservableProperty]
    private bool _canExecuteQuery;

    public event Action? ScrollToBottomRequested;

    public bool CanSendMessage => !IsProcessing && IsChatInitialized && !string.IsNullOrWhiteSpace(InputText);
    
    public bool CanSaveQuery => !string.IsNullOrWhiteSpace(GeneratedSql);

    public AiQueryViewModel() : this(
        new ConnectionStorageService(),
        new SchemaService(),
        new ChatService(),
        new ConversationStore(),
        new McpServerManager(),
        new PromptTemplateService(),
        new SavedSqlQueryService())
    {
    }

    public AiQueryViewModel(
        ConnectionStorageService connectionStorageService,
        SchemaService schemaService,
        ChatService chatService,
        ConversationStore conversationStore,
        McpServerManager mcpServerManager,
        PromptTemplateService promptTemplateService,
        SavedSqlQueryService savedSqlQueryService)
    {
        _connectionStorageService = connectionStorageService;
        _schemaService = schemaService;
        _chatService = chatService;
        _conversationStore = conversationStore;
        _mcpServerManager = mcpServerManager;
        _promptTemplateService = promptTemplateService;
        _savedSqlQueryService = savedSqlQueryService;

        // Wire up MCP to chat service
        _chatService.SetMcpServerManager(_mcpServerManager);

        // Wire up chat service events
        _chatService.OnStreamingDelta += OnStreamingDelta;
        _chatService.OnProcessingStarted += OnProcessingStarted;
        _chatService.OnProcessingCompleted += OnProcessingCompleted;
        _chatService.OnProcessingError += OnProcessingError;
        _chatService.OnProcessingCancelled += OnProcessingCancelled;
        _chatService.OnUsageReceived += OnUsageReceived;
        _chatService.OnToolApprovalRequired += OnToolApprovalRequired;
        _chatService.OnToolInvoked += OnToolInvoked;
        _chatService.OnToolCompleted += OnToolCompleted;

        // Wire up MCP server status changes
        _mcpServerManager.ServerStatusChanged += OnMcpServerStatusChanged;

        Messages.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasMessages));

        // Initialize
        _ = InitializeAsync();
    }

    internal async Task InitializeAsync()
    {
        await LoadConnectionsAsync();
        await LoadSettingsAsync();
        await LoadPromptTemplatesAsync();
        await LoadSavedQueriesAsync();
        await LoadConversationHistoryAsync();
        await InitializeMcpServersAsync();
        InitializeModels();
    }

    private async Task LoadPromptTemplatesAsync()
    {
        try
        {
            var templates = await _promptTemplateService.GetAllTemplatesAsync();
            AvailableTemplates.Clear();
            foreach (var template in templates)
            {
                AvailableTemplates.Add(template);
            }

            // Select the default SQL assistant template
            SelectedTemplate = AvailableTemplates.FirstOrDefault(t => t.Id == "sql-assistant") 
                              ?? AvailableTemplates.FirstOrDefault();
        }
        catch (Exception ex)
        {
            SetError($"Failed to load templates: {ex.Message}");
        }
    }

    private async Task LoadSavedQueriesAsync()
    {
        try
        {
            var queries = await _savedSqlQueryService.LoadAllQueriesAsync();
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
    }

    private async Task LoadConversationHistoryAsync()
    {
        try
        {
            var conversations = await _conversationStore.LoadAllConversationsAsync();
            ConversationHistory.Clear();
            foreach (var conv in conversations.OrderByDescending(c => c.UpdatedAt))
            {
                ConversationHistory.Add(conv);
            }
        }
        catch (Exception ex)
        {
            SetError($"Failed to load conversation history: {ex.Message}");
        }
    }

    private async Task InitializeMcpServersAsync()
    {
        try
        {
            await _mcpServerManager.LoadServersAsync();
            await _mcpServerManager.InitializeAllServersAsync();
            UpdateMcpStatus();
        }
        catch (Exception ex)
        {
            SetError($"Failed to initialize MCP servers: {ex.Message}");
        }
    }

    private void UpdateMcpStatus()
    {
        ConnectedMcpServers = _mcpServerManager.ServerViewModels.Count(vm => vm.Value.Status == McpServerStatus.Connected);
        AvailableMcpTools = _mcpServerManager.GetAllTools().Count;
        IsMcpEnabled = ConnectedMcpServers > 0;
    }

    private void OnMcpServerStatusChanged(string serverLabel, McpServerStatus status, string? errorMessage)
    {
        Application.Current.Dispatcher.Invoke(UpdateMcpStatus);
    }

    private void OnToolApprovalRequired(ToolCallRequest request)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Generate the tool key for tracking (with optional server label)
            var toolKey = GetToolKey(request);
            
            // Check if this tool is always allowed
            if (_alwaysAllowedTools.Contains(toolKey))
            {
                request.Status = ToolApprovalStatus.AutoApproved;
                request.ApprovalTask?.SetResult(true);
                ResultStatusText = $"Auto-approved tool: {request.ToolName}";
                return;
            }
            
            // Check if this tool is always denied
            if (_alwaysDeniedTools.Contains(toolKey))
            {
                request.Status = ToolApprovalStatus.Denied;
                request.ApprovalTask?.SetResult(false);
                ResultStatusText = $"Auto-denied tool: {request.ToolName}";
                return;
            }
            
            // Show approval dialog for manual decision
            PendingToolApproval = request;
            IsToolApprovalVisible = true;
        });
    }
    
    /// <summary>
    /// Generates a unique key for tracking tool approval decisions.
    /// Format: "serverLabel:toolName" for MCP tools, or just "toolName" for built-in tools.
    /// </summary>
    private static string GetToolKey(ToolCallRequest request)
    {
        return string.IsNullOrEmpty(request.ServerLabel) 
            ? request.ToolName 
            : $"{request.ServerLabel}:{request.ToolName}";
    }

    private void OnToolInvoked(string toolName)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ResultStatusText = $"Tool invoked: {toolName}";
        });
    }

    private void OnToolCompleted(string toolName, string result)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ResultStatusText = $"Tool completed: {toolName}";
        });
    }

    partial void OnSelectedTemplateChanged(PromptTemplate? value)
    {
        if (value != null)
        {
            _ = ReinitializeChatWithSchemaAsync();
        }
    }

    partial void OnSelectedSavedQueryChanged(SavedSqlQuery? value)
    {
        if (value != null)
        {
            GeneratedSql = value.SqlText;
            QueryName = value.Name;
            UpdateCanExecuteQuery();
        }
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

    private async Task LoadSettingsAsync()
    {
        _settings = await _conversationStore.LoadSettingsAsync();
        UpdateApiKeyStatus();
    }

    private void InitializeModels()
    {
        AvailableModels.Clear();
        foreach (var model in ModelOption.GetDefaultModels())
        {
            AvailableModels.Add(model);
        }

        // Add custom models from settings
        foreach (var custom in _settings.CustomModels)
        {
            AvailableModels.Add(new ModelOption
            {
                Id = custom.Id,
                DisplayName = custom.DisplayName,
                ProviderEnum = custom.Provider,
                ApiName = custom.ApiName,
                MaxContextTokens = custom.MaxContextTokens,
                IsCustom = true
            });
        }

        // Select default or previously selected model
        SelectedModel = AvailableModels.FirstOrDefault(m => m.Id == _settings.SelectedModelId)
                        ?? AvailableModels.FirstOrDefault();
    }

    private void UpdateApiKeyStatus()
    {
        IsApiKeyConfigured = _settings.ApiKeys.Count > 0 && 
                             _settings.ApiKeys.Any(kvp => !string.IsNullOrWhiteSpace(kvp.Value));
        
        if (IsApiKeyConfigured)
        {
            var configuredCount = _settings.ApiKeys.Count(kvp => !string.IsNullOrWhiteSpace(kvp.Value));
            ApiKeyStatusText = $"{configuredCount} provider(s) configured";
        }
        else
        {
            ApiKeyStatusText = "No API keys configured";
        }
    }

    partial void OnSelectedConnectionChanged(DatabaseConnection? value)
    {
        IsConnected = value != null;
        ConnectionStatusText = value != null
            ? $"Connected to {value.Name}"
            : "No database connected";

        AvailableTables.Clear();
        SelectedTable = null;
        AvailableColumns.Clear();
        SchemaPreview = "Loading schema...";

        if (value != null)
        {
            _ = LoadTablesAndSchemaAsync();
        }
        else
        {
            SchemaPreview = "Select a database to see schema...";
        }

        UpdateCanExecuteQuery();
        _ = ReinitializeChatWithSchemaAsync();
    }

    partial void OnSelectedTableChanged(DatabaseObject? value)
    {
        AvailableColumns.Clear();

        if (value != null)
        {
            _ = LoadColumnsAsync();
            _ = UpdateSchemaPreviewAsync();
        }

        UpdateCanExecuteQuery();
    }

    partial void OnSelectedModelChanged(ModelOption? value)
    {
        if (value != null)
        {
            _settings.SelectedModelId = value.Id;
            _ = _conversationStore.SaveSettingsAsync(_settings);
            _ = ReinitializeChatWithSchemaAsync();
        }
    }

    private async Task LoadTablesAndSchemaAsync()
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

            await UpdateSchemaPreviewAsync();
        }
        catch (Exception ex)
        {
            SetError($"Failed to load tables: {ex.Message}");
            SchemaPreview = $"Error loading schema: {ex.Message}";
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

    private async Task UpdateSchemaPreviewAsync()
    {
        if (SelectedConnection == null)
        {
            SchemaPreview = "Select a database to see schema...";
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Database: {SelectedConnection.Name} ({SelectedConnection.DatabaseType})");
        sb.AppendLine();

        if (SelectedTable != null)
        {
            // Show focused table schema
            sb.AppendLine($"Table: {SelectedTable.DisplayName}");
            sb.AppendLine("Columns:");
            
            if (AvailableColumns.Count > 0)
            {
                foreach (var col in AvailableColumns)
                {
                    var nullable = col.IsNullable ? "NULL" : "NOT NULL";
                    sb.AppendLine($"  - {col.Name} ({col.DataType}) {nullable}");
                }
            }
            else
            {
                // Load columns if not already loaded
                try
                {
                    var columns = await _schemaService.GetColumnsAsync(
                        SelectedConnection,
                        SelectedTable.Name,
                        string.IsNullOrEmpty(SelectedTable.Schema) ? null : SelectedTable.Schema);

                    foreach (var col in columns)
                    {
                        var nullable = col.IsNullable ? "NULL" : "NOT NULL";
                        sb.AppendLine($"  - {col.Name} ({col.DataType}) {nullable}");
                        AvailableColumns.Add(col);
                    }
                }
                catch
                {
                    sb.AppendLine("  (Unable to load columns)");
                }
            }
        }
        else
        {
            // Show all tables summary
            sb.AppendLine($"Tables ({AvailableTables.Count}):");
            foreach (var table in AvailableTables.Take(15))
            {
                sb.AppendLine($"  • {table.DisplayName}");
            }
            if (AvailableTables.Count > 15)
            {
                sb.AppendLine($"  ... and {AvailableTables.Count - 15} more");
            }
        }

        SchemaPreview = sb.ToString();
    }

    private string BuildSystemPrompt()
    {
        var sb = new StringBuilder();
        
        // Use selected template content if available, otherwise use default
        if (SelectedTemplate != null)
        {
            sb.AppendLine(SelectedTemplate.Content);
        }
        else
        {
            sb.AppendLine("You are a SQL query assistant. Your role is to help users generate SQL queries based on their natural language questions.");
            sb.AppendLine();
            sb.AppendLine("IMPORTANT INSTRUCTIONS:");
            sb.AppendLine("1. When generating SQL, output ONLY the SQL query wrapped in a code block with ```sql markers.");
            sb.AppendLine("2. Before the SQL, you may provide a brief explanation (1-2 sentences max).");
            sb.AppendLine("3. Use proper SQL syntax for the database type specified.");
            sb.AppendLine("4. Always use explicit column names instead of SELECT *.");
            sb.AppendLine("5. Include appropriate WHERE clauses, ORDER BY, and LIMIT/TOP as needed.");
        }
        
        sb.AppendLine();

        if (SelectedConnection != null)
        {
            sb.AppendLine($"DATABASE TYPE: {SelectedConnection.DatabaseType}");
            sb.AppendLine();
            sb.AppendLine("AVAILABLE SCHEMA:");
            sb.AppendLine(SchemaPreview);
        }

        return sb.ToString();
    }

    private async Task ReinitializeChatWithSchemaAsync()
    {
        if (!IsApiKeyConfigured || SelectedModel == null)
        {
            IsChatInitialized = false;
            return;
        }

        try
        {
            var systemPrompt = BuildSystemPrompt();
            await _chatService.InitializeAsync(_settings, systemPrompt, SelectedModel);
            IsChatInitialized = true;
            ClearError();
        }
        catch (Exception ex)
        {
            SetError($"Failed to initialize AI: {ex.Message}");
            IsChatInitialized = false;
        }
    }

    private void UpdateCanExecuteQuery()
    {
        CanExecuteQuery = IsConnected && !string.IsNullOrWhiteSpace(GeneratedSql);
        OnPropertyChanged(nameof(CanSaveQuery));
    }

    #region Commands

    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText) || !IsChatInitialized)
            return;

        var userMessageText = InputText.Trim();
        InputText = string.Empty;

        // Add user message to UI
        var userMessage = new MessageViewModel
        {
            Role = MessageRole.User,
            Content = userMessageText
        };
        Messages.Add(userMessage);
        ScrollToBottomRequested?.Invoke();

        try
        {
            _requestStopwatch.Restart();
            await _chatService.SendMessageAsync(userMessageText);
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            _requestStopwatch.Stop();
        }
    }

    [RelayCommand(CanExecute = nameof(IsProcessing))]
    private void Cancel()
    {
        _chatService.CancelRequest();
    }

    [RelayCommand]
    private void ClearChat()
    {
        Messages.Clear();
        _chatService.ClearConversation();
        GeneratedSql = string.Empty;
        QueryResults = null;
        ResultStatusText = "Ready - ask a question to generate SQL";
        _ = ReinitializeChatWithSchemaAsync();
    }

    [RelayCommand]
    private async Task RefreshSchemaAsync()
    {
        if (SelectedConnection == null) return;
        await LoadTablesAndSchemaAsync();
        await ReinitializeChatWithSchemaAsync();
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var settingsWindow = new Views.AiSettingsWindow(_settings, _conversationStore);
        settingsWindow.Owner = Application.Current.MainWindow;
        
        if (settingsWindow.ShowDialog() == true)
        {
            // Reload settings after dialog closes
            _settings = settingsWindow.GetSettings();
            await LoadSettingsAsync();
            await ReinitializeChatWithSchemaAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteQuery))]
    private async Task ExecuteQueryAsync()
    {
        if (SelectedConnection == null || string.IsNullOrWhiteSpace(GeneratedSql))
        {
            SetError("Cannot execute: no connection or SQL");
            return;
        }

        IsLoading = true;
        ClearError();
        ResultStatusText = "Executing query...";

        try
        {
            QueryResults = await _schemaService.ExecuteQueryAsync(SelectedConnection, GeneratedSql);
            ResultStatusText = $"Query returned {QueryResults.Rows.Count} row(s)";
        }
        catch (Exception ex)
        {
            SetError($"Query failed: {ex.Message}");
            ResultStatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void CopySql()
    {
        if (!string.IsNullOrWhiteSpace(GeneratedSql))
        {
            Clipboard.SetText(GeneratedSql);
            ResultStatusText = "SQL copied to clipboard";
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveQuery))]
    private async Task SaveQueryAsync()
    {
        if (string.IsNullOrWhiteSpace(GeneratedSql))
        {
            SetError("No SQL to save");
            return;
        }

        // Generate a name if not provided
        var name = string.IsNullOrWhiteSpace(QueryName) 
            ? $"Query {DateTime.Now:yyyy-MM-dd HH:mm}" 
            : QueryName;

        var savedQuery = new SavedSqlQuery
        {
            Name = name,
            SqlText = GeneratedSql,
            DatabaseConnection = SelectedConnection?.Name,
            PromptUsed = Messages.LastOrDefault(m => m.Role == MessageRole.User)?.Content,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await _savedSqlQueryService.SaveQueryAsync(savedQuery);
            SavedQueries.Insert(0, savedQuery);
            ResultStatusText = $"Query saved as '{name}'";
            QueryName = string.Empty;
        }
        catch (Exception ex)
        {
            SetError($"Failed to save query: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteSavedQueryAsync(SavedSqlQuery? query)
    {
        if (query == null) return;

        try
        {
            await _savedSqlQueryService.DeleteQueryAsync(query.Id);
            SavedQueries.Remove(query);
            
            if (SelectedSavedQuery == query)
            {
                SelectedSavedQuery = null;
            }
            
            ResultStatusText = $"Query '{query.Name}' deleted";
        }
        catch (Exception ex)
        {
            SetError($"Failed to delete query: {ex.Message}");
        }
    }

    [RelayCommand]
    private void LoadSavedQuery(SavedSqlQuery? query)
    {
        if (query == null) return;
        
        GeneratedSql = query.SqlText;
        QueryName = query.Name;
        UpdateCanExecuteQuery();
        ResultStatusText = $"Loaded query: {query.Name}";
    }

    [RelayCommand]
    private void ApproveToolCall()
    {
        if (PendingToolApproval?.ApprovalTask != null)
        {
            // If the user checked "Remember Decision" or this is "Always Allow"
            if (PendingToolApproval.RememberDecision || PendingToolApproval.AlwaysAllow)
            {
                var toolKey = GetToolKey(PendingToolApproval);
                _alwaysAllowedTools.Add(toolKey);
                _alwaysDeniedTools.Remove(toolKey); // Remove from denied if it was there
            }
            
            PendingToolApproval.Status = ToolApprovalStatus.Approved;
            PendingToolApproval.ApprovalTask.SetResult(true);
            IsToolApprovalVisible = false;
            PendingToolApproval = null;
        }
    }
    
    /// <summary>
    /// Approves the current tool call and marks it to always be allowed in the future.
    /// </summary>
    [RelayCommand]
    private void AlwaysAllowToolCall()
    {
        if (PendingToolApproval != null)
        {
            PendingToolApproval.AlwaysAllow = true;
            ApproveToolCall();
        }
    }

    [RelayCommand]
    private void DenyToolCall()
    {
        if (PendingToolApproval?.ApprovalTask != null)
        {
            // If the user checked "Remember Decision"
            if (PendingToolApproval.RememberDecision)
            {
                var toolKey = GetToolKey(PendingToolApproval);
                _alwaysDeniedTools.Add(toolKey);
                _alwaysAllowedTools.Remove(toolKey); // Remove from allowed if it was there
            }
            
            PendingToolApproval.Status = ToolApprovalStatus.Denied;
            PendingToolApproval.ApprovalTask.SetResult(false);
            IsToolApprovalVisible = false;
            PendingToolApproval = null;
        }
    }
    
    /// <summary>
    /// Clears all remembered tool approval decisions, requiring approval again for all tools.
    /// </summary>
    [RelayCommand]
    private void ClearToolApprovalMemory()
    {
        _alwaysAllowedTools.Clear();
        _alwaysDeniedTools.Clear();
        ResultStatusText = "Tool approval memory cleared";
    }
    
    /// <summary>
    /// Gets the count of tools that are set to always allow.
    /// </summary>
    public int AlwaysAllowedToolCount => _alwaysAllowedTools.Count;
    
    /// <summary>
    /// Gets whether any tools have remembered approval decisions.
    /// </summary>
    public bool HasRememberedToolDecisions => _alwaysAllowedTools.Count > 0 || _alwaysDeniedTools.Count > 0;

    [RelayCommand]
    private async Task RefreshMcpServersAsync()
    {
        try
        {
            await _mcpServerManager.InitializeAllServersAsync();
            UpdateMcpStatus();
            
            // Reinitialize chat to pick up new tools
            await ReinitializeChatWithSchemaAsync();
            ResultStatusText = $"MCP servers refreshed: {ConnectedMcpServers} connected, {AvailableMcpTools} tools available";
        }
        catch (Exception ex)
        {
            SetError($"Failed to refresh MCP servers: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveConversationAsync()
    {
        if (Messages.Count == 0)
        {
            SetError("No conversation to save");
            return;
        }

        try
        {
            var conversation = new ConversationModel
            {
                Title = Messages.FirstOrDefault(m => m.Role == MessageRole.User)?.Content?.Substring(0, Math.Min(50, Messages.First(m => m.Role == MessageRole.User)?.Content?.Length ?? 0)) ?? "New Conversation",
                Messages = Messages.Select(m => new ChatMessageModel
                {
                    Role = m.Role,
                    Content = m.Content ?? string.Empty,
                    Timestamp = DateTime.UtcNow,
                    TokenCount = m.TokenCount
                }).ToList(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _conversationStore.SaveConversationAsync(conversation);
            ConversationHistory.Insert(0, conversation);
            ResultStatusText = "Conversation saved";
        }
        catch (Exception ex)
        {
            SetError($"Failed to save conversation: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task LoadConversationAsync(ConversationModel? conversation)
    {
        if (conversation == null) return;

        try
        {
            Messages.Clear();
            foreach (var msg in conversation.Messages)
            {
                Messages.Add(new MessageViewModel
                {
                    Role = msg.Role,
                    Content = msg.Content ?? string.Empty
                });
            }
            
            SelectedConversation = conversation;
            ResultStatusText = $"Loaded conversation: {conversation.Title}";
        }
        catch (Exception ex)
        {
            SetError($"Failed to load conversation: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteConversationAsync(ConversationModel? conversation)
    {
        if (conversation == null) return;

        try
        {
            await _conversationStore.DeleteConversationAsync(conversation.Id);
            ConversationHistory.Remove(conversation);
            
            if (SelectedConversation == conversation)
            {
                SelectedConversation = null;
            }
            
            ResultStatusText = "Conversation deleted";
        }
        catch (Exception ex)
        {
            SetError($"Failed to delete conversation: {ex.Message}");
        }
    }

    #endregion

    #region Chat Service Event Handlers

    private void OnStreamingDelta(string delta)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _currentStreamingMessage?.AppendContent(delta);
            ScrollToBottomRequested?.Invoke();
        });
    }

    private void OnProcessingStarted()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsProcessing = true;
            ResultStatusText = "AI is thinking...";
            ClearError();

            _currentStreamingMessage = new MessageViewModel
            {
                Role = MessageRole.Assistant
            };
            _currentStreamingMessage.StartStreaming();
            Messages.Add(_currentStreamingMessage);

            SendMessageCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        });
    }

    private void OnProcessingCompleted(LlmTornado.Chat.ChatMessage response)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsProcessing = false;
            ResultStatusText = $"Response received in {_requestStopwatch.ElapsedMilliseconds}ms";

            if (_currentStreamingMessage != null)
            {
                _currentStreamingMessage.EndStreaming();

                if (string.IsNullOrEmpty(_currentStreamingMessage.Content))
                {
                    _currentStreamingMessage.Content = response.Content ?? string.Empty;
                }

                // Extract SQL from the response
                ExtractSqlFromResponse(_currentStreamingMessage.Content);
            }

            _currentStreamingMessage = null;

            SendMessageCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
            ExecuteQueryCommand.NotifyCanExecuteChanged();
            ScrollToBottomRequested?.Invoke();
        });
    }

    private void ExtractSqlFromResponse(string content)
    {
        // Look for SQL code blocks
        var sqlPattern = @"```sql\s*([\s\S]*?)\s*```";
        var match = System.Text.RegularExpressions.Regex.Match(content, sqlPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            GeneratedSql = match.Groups[1].Value.Trim();
            UpdateCanExecuteQuery();
            ResultStatusText = "SQL extracted - ready to execute";
        }
        else
        {
            // Try to find any code block
            var codePattern = @"```\s*([\s\S]*?)\s*```";
            match = System.Text.RegularExpressions.Regex.Match(content, codePattern);
            if (match.Success)
            {
                var code = match.Groups[1].Value.Trim();
                // Check if it looks like SQL
                if (code.Contains("SELECT", StringComparison.OrdinalIgnoreCase) ||
                    code.Contains("INSERT", StringComparison.OrdinalIgnoreCase) ||
                    code.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) ||
                    code.Contains("DELETE", StringComparison.OrdinalIgnoreCase))
                {
                    GeneratedSql = code;
                    UpdateCanExecuteQuery();
                    ResultStatusText = "SQL extracted - ready to execute";
                }
            }
        }
    }

    private void OnProcessingError(Exception ex)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsProcessing = false;
            SetError(ex.Message);
            ResultStatusText = "Error occurred";

            if (_currentStreamingMessage != null)
            {
                _currentStreamingMessage.EndStreaming();
                _currentStreamingMessage.Content += $"\n\n[Error: {ex.Message}]";
            }

            _currentStreamingMessage = null;

            SendMessageCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        });
    }

    private void OnProcessingCancelled()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsProcessing = false;
            ResultStatusText = "Cancelled";

            if (_currentStreamingMessage != null)
            {
                _currentStreamingMessage.EndStreaming();
                _currentStreamingMessage.Content += "\n\n[Cancelled by user]";
            }

            _currentStreamingMessage = null;

            SendMessageCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        });
    }

    private void OnUsageReceived(int inputTokens, int outputTokens, int totalTokens)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_currentStreamingMessage != null)
            {
                _currentStreamingMessage.TokenCount = outputTokens;
            }
        });
    }

    #endregion
}
