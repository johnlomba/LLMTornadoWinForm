using System.Text.RegularExpressions;
using SqlKata;
using SqlKata.Compilers;
using SmarterViews.Desktop.Models;

namespace SmarterViews.Desktop.Services;

/// <summary>
/// Service for building SQL queries using SqlKata
/// </summary>
public class QueryService
{
    private readonly Compiler _compiler;

    public QueryService(Compiler? compiler = null)
    {
        // Default to SQL Server compiler, can be replaced with PostgresCompiler, MySqlCompiler, etc.
        _compiler = compiler ?? new SqlServerCompiler();
    }

    /// <summary>
    /// Creates a new query for the specified table
    /// </summary>
    public Query CreateQuery(string tableName)
    {
        return new Query(tableName);
    }

    /// <summary>
    /// Applies rules from a RuleSet to a query
    /// </summary>
    public Query ApplyRules(Query query, RuleSet? rules)
    {
        if (rules?.Rules == null || rules.Rules.Count == 0)
        {
            return query;
        }

        foreach (var rule in rules.Rules)
        {
            query = ApplyRule(query, rule);
        }

        return query;
    }

    /// <summary>
    /// Applies a single rule to a query
    /// </summary>
    private Query ApplyRule(Query query, QueryRule rule)
    {
        if (string.IsNullOrEmpty(rule.Field))
        {
            return query;
        }

        return rule.Operator.ToUpperInvariant() switch
        {
            "=" or "EQUALS" => query.Where(rule.Field, rule.Value),
            "!=" or "<>" or "NOT_EQUALS" => query.WhereNot(rule.Field, rule.Value),
            ">" => query.Where(rule.Field, ">", rule.Value),
            ">=" => query.Where(rule.Field, ">=", rule.Value),
            "<" => query.Where(rule.Field, "<", rule.Value),
            "<=" => query.Where(rule.Field, "<=", rule.Value),
            "LIKE" or "CONTAINS" => query.WhereLike(rule.Field, $"%{rule.Value}%"),
            "STARTS_WITH" => query.WhereLike(rule.Field, $"{rule.Value}%"),
            "ENDS_WITH" => query.WhereLike(rule.Field, $"%{rule.Value}"),
            "IS_NULL" => query.WhereNull(rule.Field),
            "IS_NOT_NULL" => query.WhereNotNull(rule.Field),
            "IN" when rule.Value is IEnumerable<object> values => query.WhereIn(rule.Field, values),
            _ => query.Where(rule.Field, rule.Operator, rule.Value)
        };
    }

    /// <summary>
    /// Compiles a query to SQL with values inlined (no parameters)
    /// This makes the SQL copyable and directly executable
    /// </summary>
    public SqlResult Compile(Query query)
    {
        var result = _compiler.Compile(query);
        
        // Create a new SqlResult with inlined values
        var inlinedSql = InlineParameters(result.Sql, result.Bindings);
        
        return new SqlResult 
        { 
            Sql = inlinedSql,
            RawSql = result.RawSql,
            Bindings = [] // No bindings since values are inlined
        };
    }

    /// <summary>
    /// Replaces parameter placeholders with actual values
    /// </summary>
    private string InlineParameters(string sql, List<object> bindings)
    {
        if (bindings == null || bindings.Count == 0)
        {
            return sql;
        }

        var result = sql;
        
        // Replace each parameter placeholder with the actual value
        for (int i = bindings.Count - 1; i >= 0; i--)
        {
            var placeholder = $"@p{i}";
            var value = FormatValue(bindings[i]);
            result = result.Replace(placeholder, value);
        }

        return result;
    }

    /// <summary>
    /// Formats a value for SQL inline insertion
    /// </summary>
    private string FormatValue(object? value)
    {
        if (value == null)
        {
            return "NULL";
        }

        return value switch
        {
            string s => $"'{EscapeString(s)}'",
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
            DateTimeOffset dto => $"'{dto:yyyy-MM-dd HH:mm:ss}'",
            bool b => b ? "1" : "0",
            decimal or double or float or int or long or short or byte => value.ToString() ?? "NULL",
            _ => $"'{EscapeString(value.ToString() ?? string.Empty)}'"
        };
    }

    /// <summary>
    /// Escapes single quotes in strings for SQL
    /// </summary>
    private string EscapeString(string value)
    {
        return value.Replace("'", "''");
    }

    /// <summary>
    /// Builds a complete query from a QueryDefinition
    /// </summary>
    public Query BuildFromDefinition(QueryDefinition definition)
    {
        var query = CreateQuery(definition.Table);

        if (definition.SelectedColumns.Count > 0)
        {
            query = query.Select(definition.SelectedColumns.ToArray());
        }

        if (definition.Rules != null)
        {
            query = ApplyRules(query, definition.Rules);
        }

        return query;
    }
}
