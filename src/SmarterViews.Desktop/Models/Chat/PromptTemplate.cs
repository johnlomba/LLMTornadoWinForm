using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SmarterViews.Desktop.Models.Chat;

/// <summary>
/// Represents a system prompt template.
/// </summary>
public class PromptTemplate : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = string.Empty;
    private string _content = string.Empty;
    private string? _description;
    private bool _isBuiltIn;
    private DateTime _createdAt = DateTime.UtcNow;
    
    /// <summary>
    /// Unique identifier for the template.
    /// </summary>
    public string Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }
    
    /// <summary>
    /// Display name for the template.
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }
    
    /// <summary>
    /// The actual system prompt content.
    /// </summary>
    public string Content
    {
        get => _content;
        set => SetField(ref _content, value);
    }
    
    /// <summary>
    /// Optional description of what this template is for.
    /// </summary>
    public string? Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }
    
    /// <summary>
    /// Whether this is a built-in template or user-created.
    /// </summary>
    public bool IsBuiltIn
    {
        get => _isBuiltIn;
        set => SetField(ref _isBuiltIn, value);
    }
    
    /// <summary>
    /// When the template was created.
    /// </summary>
    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetField(ref _createdAt, value);
    }
    
    /// <summary>
    /// Event raised when a property changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Creates default built-in templates for SQL generation.
    /// </summary>
    public static List<PromptTemplate> GetBuiltInTemplates()
    {
        return
        [
            new PromptTemplate
            {
                Id = "sql-assistant",
                Name = "SQL Assistant (Default)",
                Content = """
                    You are a SQL query assistant. Your role is to help users generate SQL queries based on their natural language questions.

                    IMPORTANT INSTRUCTIONS:
                    1. When generating SQL, output ONLY the SQL query wrapped in a code block with ```sql markers.
                    2. Before the SQL, you may provide a brief explanation (1-2 sentences max).
                    3. Use proper SQL syntax for the database type specified.
                    4. Always use explicit column names instead of SELECT *.
                    5. Include appropriate WHERE clauses, ORDER BY, and LIMIT/TOP as needed.
                    6. For complex queries, use CTEs (Common Table Expressions) for readability.
                    7. Always consider performance - avoid unnecessary JOINs and subqueries.
                    """,
                Description = "Generates SQL queries from natural language with best practices.",
                IsBuiltIn = true
            },
            new PromptTemplate
            {
                Id = "sql-optimizer",
                Name = "SQL Optimizer",
                Content = """
                    You are a SQL optimization expert. Your role is to analyze and optimize SQL queries for better performance.

                    INSTRUCTIONS:
                    1. Analyze the provided SQL query for performance issues.
                    2. Suggest indexes that could improve query performance.
                    3. Rewrite queries to be more efficient when possible.
                    4. Explain the reasoning behind each optimization.
                    5. Consider the database type when making recommendations.
                    6. Always output optimized SQL in ```sql code blocks.
                    """,
                Description = "Analyzes and optimizes SQL queries for better performance.",
                IsBuiltIn = true
            },
            new PromptTemplate
            {
                Id = "sql-explainer",
                Name = "SQL Explainer",
                Content = """
                    You are a SQL educator. Your role is to explain SQL queries and database concepts clearly.

                    INSTRUCTIONS:
                    1. Break down complex queries into understandable parts.
                    2. Explain what each clause does (SELECT, FROM, WHERE, JOIN, etc.).
                    3. Use simple language suitable for beginners.
                    4. Provide examples when helpful.
                    5. If generating SQL, always wrap it in ```sql code blocks.
                    """,
                Description = "Explains SQL queries and database concepts in simple terms.",
                IsBuiltIn = true
            },
            new PromptTemplate
            {
                Id = "data-analyst",
                Name = "Data Analyst",
                Content = """
                    You are a data analyst expert. Help users explore and analyze their data using SQL.

                    INSTRUCTIONS:
                    1. Generate analytical queries for data exploration.
                    2. Include aggregations, groupings, and statistical functions.
                    3. Suggest relevant data visualizations based on query results.
                    4. Help identify trends, patterns, and anomalies in data.
                    5. Always output SQL in ```sql code blocks.
                    6. Provide insights about what the data might reveal.
                    """,
                Description = "Expert data analysis and statistics assistance.",
                IsBuiltIn = true
            },
            new PromptTemplate
            {
                Id = "schema-designer",
                Name = "Schema Designer",
                Content = """
                    You are a database schema design expert. Help users design and modify database schemas.

                    INSTRUCTIONS:
                    1. Generate CREATE TABLE, ALTER TABLE, and other DDL statements.
                    2. Apply normalization best practices.
                    3. Recommend appropriate data types for each column.
                    4. Design proper primary keys, foreign keys, and indexes.
                    5. Consider referential integrity constraints.
                    6. Always output SQL in ```sql code blocks.
                    """,
                Description = "Designs and modifies database schemas with best practices.",
                IsBuiltIn = true
            },
            new PromptTemplate
            {
                Id = "report-builder",
                Name = "Report Builder",
                Content = """
                    You are a business intelligence report builder. Help users create reports and dashboards with SQL.

                    INSTRUCTIONS:
                    1. Generate queries for business reports (sales, inventory, customers, etc.).
                    2. Include date range filters and groupings.
                    3. Calculate KPIs, metrics, and year-over-year comparisons.
                    4. Use appropriate formatting for currency, percentages, and dates.
                    5. Always output SQL in ```sql code blocks.
                    6. Suggest how results could be visualized in charts or tables.
                    """,
                Description = "Creates business intelligence reports and dashboards.",
                IsBuiltIn = true
            }
        ];
    }
}
