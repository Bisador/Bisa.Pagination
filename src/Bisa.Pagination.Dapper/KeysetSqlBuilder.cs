using Bisa.Pagination.Abstractions;
using Bisa.Pagination.Abstractions.Enums;
using Bisa.Pagination.Abstractions.Exceptions;

namespace Bisa.Pagination.Dapper;

/// <summary>The output of creating the Keyset condition: WHERE part (parameter), ORDER BY and parameter values.</summary>
public sealed class KeysetSqlFragment
{
    /// <summary>If it is null (the first page), there is no need to AND the condition.</summary>
    public string? WhereClause { get; init; }

    public string OrderByClause { get; init; } = "";

    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();
}

/// <summary>
/// Raw SQL equivalent of Keyset Predicate algorithm in Bisa.Pagination.Core,
/// but based on the column name (string) instead of the Expression Tree - because Dapper works with raw SQL.
/// </summary>
public static class KeysetSqlBuilder
{
    public static KeysetSqlFragment Build(
        IReadOnlyList<SortField> sortFields,
        CursorPosition? position,
        PaginationDirection direction,
        string? tableAlias = null,
        string parameterPrefix = "bp_")
    {
        if (sortFields is null || sortFields.Count == 0)
            throw new ArgumentException("At least one SortField is required.", nameof(sortFields));

        var prefix = string.IsNullOrEmpty(tableAlias) ? "" : $"{tableAlias}.";

        var orderFields = direction == PaginationDirection.Backward
            ? sortFields.Select(f => f with { Direction = Flip(f.Direction) }).ToList()
            : sortFields;

        var orderByClause = string.Join(", ",
            orderFields.Select(f => $"{prefix}{f.Name} {(f.Direction == SortDirection.Ascending ? "ASC" : "DESC")}"));

        if (position is null)
            return new KeysetSqlFragment { WhereClause = null, OrderByClause = orderByClause };

        if (position.Keys.Count != sortFields.Count)
            throw new CursorSchemaMismatchException();

        var parameters = new Dictionary<string, object?>();
        var orClauses = new List<string>();

        for (var i = 0; i < sortFields.Count; i++)
        {
            var andParts = new List<string>();

            for (var j = 0; j < i; j++)
            {
                var paramName = $"{parameterPrefix}{j}";
                var val = position.Keys[j].Value;
                if (val is null)
                {
                    andParts.Add($"{prefix}{sortFields[j].Name} IS NULL");
                }
                else
                {
                    parameters[paramName] = val;
                    andParts.Add($"{prefix}{sortFields[j].Name} = @{paramName}");
                }
            }

            var isGreaterThan = ResolveOperator(sortFields[i].Direction, direction);
            var currentValue = position.Keys[i].Value;

            if (currentValue is null)
            {
                // Convention: NULL is the smallest value (NULLS FIRST).
                andParts.Add(isGreaterThan ? $"{prefix}{sortFields[i].Name} IS NOT NULL" : "1 = 0");
            }
            else
            {
                var paramName = $"{parameterPrefix}eq{i}";
                parameters[paramName] = currentValue;
                andParts.Add($"{prefix}{sortFields[i].Name} {(isGreaterThan ? ">" : "<")} @{paramName}");
            }

            orClauses.Add("(" + string.Join(" AND ", andParts) + ")");
        }

        return new KeysetSqlFragment
        {
            WhereClause = string.Join(" OR ", orClauses.Select(c => $"({c})")),
            OrderByClause = orderByClause,
            Parameters = parameters
        };
    }

    private static bool ResolveOperator(SortDirection keyDirection, PaginationDirection pageDirection)
    {
        var greaterForForward = keyDirection == SortDirection.Ascending;
        return pageDirection == PaginationDirection.Forward ? greaterForForward : !greaterForForward;
    }

    private static SortDirection Flip(SortDirection direction) =>
        direction == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
}
