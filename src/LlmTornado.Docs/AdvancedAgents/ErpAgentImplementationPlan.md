# ERP SQL Orchestration Agent Implementation Plan

> **Created**: January 9, 2026  
> **Purpose**: Build a self-correcting orchestration agent that queries ERP data via MCP servers with configurable MSSQL/MySQL backends.

## Overview

Build an orchestration-based agent in `LlmTornado.Agents.Samples` that answers manufacturing and sales questions from ERP SQL data, using existing `MCPServer` infrastructure with new MSSQL/MySQL toolkits and environment-based configuration for work/home switching.

## Key Discovery: Existing MCP Infrastructure

Your codebase already has a robust MCP implementation:

- **`MCPServer`** (`LlmTornado.Mcp/MCPServer.cs`) - Handles stdio/HTTP transport, tool discovery, environment variables
- **`MCPToolkits`** (`LlmTornado.Mcp/MCPToolkits.cs`) - Pre-built server factories (Puppeteer, Github, Gmail, etc.)
- **`McpExtensions.ToTornadoTool()`** (`LlmTornado.Mcp/Adapter.cs`) - Converts MCP tools to TornadoAgent tools

The established pattern from demos:

```csharp
MCPServer mcpServer = new MCPServer("label", command: "uvx", arguments: [...],
    environmentVariables: new Dictionary<string, string> { ... });
await mcpServer.InitializeAsync();
agent.AddTool(mcpServer.AllowedTornadoTools.ToArray());
```

---

## Database Configuration

### Work Environment (MSSQL)

```json
{
  "mssql": {
    "command": "uvx",
    "args": ["mssql_mcp_server"],
    "env": {
      "MSSQL_DRIVER": "SQL Server",
      "MSSQL_HOST": "VISUAL01",
      "MSSQL_DATABASE": "SAN",
      "MSSQL_USER": "CURSOR_AI",
      "MSSQL_PASSWORD": "Santr0n!!"
    }
  }
}
```

### Home Environment (MySQL)

```
Host: localhost
Port: 3308
User: sysadm
Password: john
```

---

## Architecture

### Orchestration Flow

```
┌─────────────────┐
│  User Question  │
└────────┬────────┘
         ▼
┌─────────────────┐
│ ErpPlannerRun.  │  Analyze question, create SQL plan
└────────┬────────┘
         ▼
┌─────────────────┐
│ SchemaInspector │  Query DB schema via MCP tools
└────────┬────────┘
         ▼
┌─────────────────┐
│ QueryExecutor   │  Execute SQL queries via MCP
└────────┬────────┘
         ▼
┌─────────────────┐
│ ReviewerRun.    │  Validate results vs question
└────────┬────────┘
         │
    ┌────┴────┐
    ▼         ▼
┌───────┐ ┌───────────┐
│SUCCESS│ │NEEDS FIX  │──▶ CorrectorRunnable ──▶ Back to Executor
└───┬───┘ └───────────┘     (max 3 attempts)
    ▼
┌─────────────────┐
│ ResponderRun.   │  Format final answer
└────────┬────────┘
         ▼
┌─────────────────┐
│ ExitRunnable    │
└─────────────────┘
```

---

## File Structure

### New Files in `LlmTornado.Mcp/`

| File | Purpose |
|------|---------|
| `MsSqlOptions.cs` | MSSQL connection options with Work preset |
| `MySqlOptions.cs` | MySQL connection options with Home preset |
| `MCPToolkits.cs` | Add `MsSql()` and `MySql()` factory methods |

### New Files in `LlmTornado.Agents.Samples/ErpAgent/`

| File | Purpose |
|------|---------|
| `ErpAgentConfiguration.cs` | Main orchestration config |
| `ErpAgentOptions.cs` | Database provider selection + presets |
| `DataModels/SqlQueryPlan.cs` | Planning phase output |
| `DataModels/QueryExecutionResult.cs` | Execution results |
| `DataModels/ReviewResult.cs` | Review verdict + correction hints |
| `States/ErpPlannerRunnable.cs` | Analyzes question, outputs SQL plan |
| `States/SchemaInspectorRunnable.cs` | Queries DB schema via MCP |
| `States/QueryExecutorRunnable.cs` | Executes SQL via MCP tools |
| `States/ReviewerRunnable.cs` | Validates results vs question |
| `States/CorrectorRunnable.cs` | Re-plans on failure (max 3 attempts) |
| `States/ResponderRunnable.cs` | Formats final answer with streaming |
| `States/ErpExitRunnable.cs` | Terminal state |

### New File in `LlmTornado.Demo/`

| File | Purpose |
|------|---------|
| `ErpAgentDemo.cs` | Demo with work/home switching |

---

## Implementation Details

### 1. MsSqlOptions.cs

```csharp
namespace LlmTornado.Mcp;

public class MsSqlOptions
{
    public string Driver { get; set; } = "SQL Server";
    public string Host { get; set; } = "localhost";
    public string Database { get; set; } = "";
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    
    /// <summary>
    /// Work environment preset (VISUAL01/SAN)
    /// </summary>
    public static MsSqlOptions Work => new()
    {
        Driver = "SQL Server",
        Host = "VISUAL01",
        Database = "SAN",
        User = "CURSOR_AI",
        Password = Environment.GetEnvironmentVariable("MSSQL_PASSWORD") ?? ""
    };
}
```

### 2. MySqlOptions.cs

```csharp
namespace LlmTornado.Mcp;

public class MySqlOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3306;
    public string Database { get; set; } = "";
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    
    /// <summary>
    /// Home environment preset (localhost:3308)
    /// </summary>
    public static MySqlOptions Home => new()
    {
        Host = "localhost",
        Port = 3308,
        Database = "san",
        User = "sysadm",
        Password = "john"
    };
}
```

### 3. MCPToolkits Additions

```csharp
/// <summary>
/// Microsoft SQL Server MCP toolkit (mssql_mcp_server)
/// </summary>
public static MCPServer MsSql(MsSqlOptions options, string[]? allowedTools = null)
{
    return new MCPServer("mssql", command: "uvx", arguments: ["mssql_mcp_server"],
        environmentVariables: new Dictionary<string, string>
        {
            ["MSSQL_DRIVER"] = options.Driver,
            ["MSSQL_HOST"] = options.Host,
            ["MSSQL_DATABASE"] = options.Database,
            ["MSSQL_USER"] = options.User,
            ["MSSQL_PASSWORD"] = options.Password
        },
        allowedTools: allowedTools);
}

/// <summary>
/// MySQL MCP toolkit (mysql-mcp-server)
/// </summary>
public static MCPServer MySql(MySqlOptions options, string[]? allowedTools = null)
{
    return new MCPServer("mysql", command: "uvx", arguments: ["mysql-mcp-server"],
        environmentVariables: new Dictionary<string, string>
        {
            ["MYSQL_HOST"] = options.Host,
            ["MYSQL_PORT"] = options.Port.ToString(),
            ["MYSQL_DATABASE"] = options.Database,
            ["MYSQL_USER"] = options.User,
            ["MYSQL_PASSWORD"] = options.Password
        },
        allowedTools: allowedTools);
}
```

### 4. ErpAgentOptions.cs

```csharp
namespace LlmTornado.Agents.Samples.ErpAgent;

public enum DatabaseProvider { MsSql, MySql }

public class ErpAgentOptions
{
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.MsSql;
    public MsSqlOptions? MsSql { get; set; }
    public MySqlOptions? MySql { get; set; }
    
    public static ErpAgentOptions ForWork() => new()
    {
        Provider = DatabaseProvider.MsSql,
        MsSql = MsSqlOptions.Work
    };
    
    public static ErpAgentOptions ForHome() => new()
    {
        Provider = DatabaseProvider.MySql,
        MySql = MySqlOptions.Home
    };
    
    public static ErpAgentOptions FromEnvironment()
    {
        var location = Environment.GetEnvironmentVariable("ERP_LOCATION");
        return location?.ToLower() switch
        {
            "work" => ForWork(),
            "home" => ForHome(),
            _ => ForWork() // default to work
        };
    }
}
```

### 5. Data Models

```csharp
// SqlQueryPlan.cs
public struct SqlQueryPlan
{
    public string OriginalQuestion { get; set; }
    public string[] TableHints { get; set; }
    public SqlQueryStep[] Steps { get; set; }
}

public struct SqlQueryStep
{
    public string Description { get; set; }
    public string Sql { get; set; }
}

// QueryExecutionResult.cs
public class QueryExecutionResult
{
    public string Data { get; set; } = "";
    public bool Success { get; set; }
    public string[] Errors { get; set; } = [];
}

// ReviewResult.cs
public class ReviewResult
{
    public bool Success { get; set; }
    public string Data { get; set; } = "";
    public string PartialData { get; set; } = "";
    public SqlQueryPlan CorrectionPlan { get; set; }
    public int Attempts { get; set; }
    public string[] Issues { get; set; } = [];
}
```

### 6. ErpAgentConfiguration (Main Orchestration)

```csharp
public class ErpAgentConfiguration : OrchestrationRuntimeConfiguration
{
    private MCPServer _mcpServer;
    
    public ErpAgentConfiguration(TornadoApi api, ErpAgentOptions options)
    {
        // Create MCP server based on provider
        _mcpServer = options.Provider switch
        {
            DatabaseProvider.MsSql => MCPToolkits.MsSql(options.MsSql!),
            DatabaseProvider.MySql => MCPToolkits.MySql(options.MySql!),
            _ => throw new ArgumentException("Unknown provider")
        };
        
        // Initialize MCP (fetches tools)
        _mcpServer.InitializeAsync().GetAwaiter().GetResult();
        
        // Create states
        var planner = new ErpPlannerRunnable(api, this);
        var inspector = new SchemaInspectorRunnable(api, _mcpServer, this);
        var executor = new QueryExecutorRunnable(api, _mcpServer, this);
        var reviewer = new ReviewerRunnable(api, this);
        var corrector = new CorrectorRunnable(api, this);
        var responder = new ResponderRunnable(api, this);
        var exit = new ErpExitRunnable(this) { AllowDeadEnd = true };
        
        // Wire transitions
        planner.AddAdvancer(inspector);
        inspector.AddAdvancer(executor);
        executor.AddAdvancer(reviewer);
        
        // Review branching
        reviewer.AddAdvancer<ReviewResult>(r => r.Success, r => r.Data, responder);
        reviewer.AddAdvancer<ReviewResult>(
            r => !r.Success && r.Attempts < 3, 
            r => r.CorrectionPlan, 
            corrector);
        reviewer.AddAdvancer<ReviewResult>(
            r => !r.Success && r.Attempts >= 3, 
            r => r.PartialData, 
            responder);
            
        corrector.AddAdvancer(executor);
        responder.AddAdvancer(exit);
        
        SetEntryRunnable(planner);
        SetRunnableWithResult(responder);
    }
}
```

---

## Demo Usage

```csharp
public static async Task RunErpAgent()
{
    var api = new TornadoApi(
        Environment.GetEnvironmentVariable("OPENAI_API_KEY"), 
        LLmProviders.OpenAi);
    
    // Auto-detect work/home from environment
    var options = ErpAgentOptions.FromEnvironment();
    
    // Or explicitly:
    // var options = ErpAgentOptions.ForWork();   // MSSQL on VISUAL01
    // var options = ErpAgentOptions.ForHome();   // MySQL on localhost:3308
    
    var config = new ErpAgentConfiguration(api, options);
    var runtime = new ChatRuntime(config);
    
    var answer = await runtime.InvokeAsync(
        new ChatMessage(ChatMessageRole.User, 
            "What were our top 5 selling products last quarter by revenue?"));
    
    Console.WriteLine(answer.Content);
}
```

---

## Environment Variables

### Work (MSSQL)

```bash
ERP_LOCATION=work
MSSQL_PASSWORD=Santr0n!!
```

### Home (MySQL)

```bash
ERP_LOCATION=home
# MySQL credentials hardcoded in preset for local dev
```

---

## MCP Server Requirements

```bash
# Install mssql_mcp_server (for work)
pip install mssql-mcp-server

# Install mysql-mcp-server (for home)  
pip install mysql-mcp-server
```

Both must be accessible via `uvx` (comes with `uv` package manager).

---

## Implementation Checklist

- [ ] Create `MsSqlOptions.cs` in `LlmTornado.Mcp/`
- [ ] Create `MySqlOptions.cs` in `LlmTornado.Mcp/`
- [ ] Add `MsSql()` and `MySql()` to `MCPToolkits.cs`
- [ ] Create `ErpAgent/` folder structure in `LlmTornado.Agents.Samples/`
- [ ] Create `ErpAgentOptions.cs`
- [ ] Create data models (`SqlQueryPlan`, `QueryExecutionResult`, `ReviewResult`)
- [ ] Create `ErpPlannerRunnable.cs`
- [ ] Create `SchemaInspectorRunnable.cs`
- [ ] Create `QueryExecutorRunnable.cs`
- [ ] Create `ReviewerRunnable.cs`
- [ ] Create `CorrectorRunnable.cs`
- [ ] Create `ResponderRunnable.cs`
- [ ] Create `ErpExitRunnable.cs`
- [ ] Create `ErpAgentConfiguration.cs`
- [ ] Create `ErpAgentDemo.cs` in `LlmTornado.Demo/`

