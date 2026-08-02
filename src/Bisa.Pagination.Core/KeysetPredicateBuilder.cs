namespace Bisa.Pagination.Core;

/// <summary>
/// The heart of the Keyset/Cursor Pagination implementation: from a set of sorting keys
/// (Composite Key) and the last known position form the standard WHERE clause of the Keyset:
///
/// (k1 > v1) OR (k1 = v1 AND k2 > v2) OR ... OR (k1 = v1 AND ... AND kn > vn)
///
/// operator direction (greater or lesser) based on the combination of each key's SortDirection and PaginationDirection
/// Request (Forward/Backward) is determined.
/// </summary>
internal static class KeysetPredicateBuilder
{
    public static IQueryable<T> ApplyOrder<T>(IQueryable<T> source, IReadOnlyList<SortSpecification<T>> specs)
    {
        IOrderedQueryable<T>? ordered = null;
        foreach (var spec in specs)
        {
            ordered = ordered is null
                ? OrderBy(source, spec, thenBy: false)
                : OrderBy(ordered, spec, thenBy: true);
        }

        return ordered ?? source;
    }

    private static IOrderedQueryable<T> OrderBy<T>(IQueryable<T> source, SortSpecification<T> spec, bool thenBy)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var member = ExpressionHelper.BuildMemberAccess(parameter, spec.PropertyName);
        var lambda = Expression.Lambda(member, parameter);

        var methodName = (thenBy, spec.Direction) switch
        {
            (false, SortDirection.Ascending) => nameof(Queryable.OrderBy),
            (false, SortDirection.Descending) => nameof(Queryable.OrderByDescending),
            (true, SortDirection.Ascending) => nameof(Queryable.ThenBy),
            (true, SortDirection.Descending) => nameof(Queryable.ThenByDescending),
            _ => throw new ArgumentOutOfRangeException()
        };

        var method = typeof(Queryable).GetMethods()
            .First(m => m.Name == methodName && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), member.Type);

        return (IOrderedQueryable<T>)method.Invoke(null, [source, lambda])!;
    }

    public static IQueryable<T> ApplyFilter<T>(
        IQueryable<T> source,
        IReadOnlyList<SortSpecification<T>> specs,
        CursorPosition position,
        PaginationDirection direction)
    {
        if (specs.Count != position.Keys.Count)
            throw new CursorSchemaMismatchException();

        var parameter = Expression.Parameter(typeof(T), "x");
        Expression? finalPredicate = null;

        for (var i = 0; i < specs.Count; i++)
        {
            Expression? clause = null;

            // Equality for all keys before i
            for (var j = 0; j < i; j++)
            {
                var eq = BuildEquality(parameter, specs[j], position.Keys[j]);
                clause = clause is null ? eq : Expression.AndAlso(clause, eq);
            }

            var isGreaterThan = ResolveOperator(specs[i].Direction, direction);
            var comparison = BuildComparisonClause(parameter, specs[i], position.Keys[i], isGreaterThan);
            clause = clause is null ? comparison : Expression.AndAlso(clause, comparison);

            finalPredicate = finalPredicate is null ? clause : Expression.OrElse(finalPredicate, clause);
        }

        var lambda = Expression.Lambda<Func<T, bool>>(finalPredicate!, parameter);
        return source.Where(lambda);
    }

    /// <summary>
    /// Specifies whether "greater than" or "less than" should be used for this key.
    /// Rule: In Forward mode, for the Ascending key, we must take the following (>)
    /// and vice versa for Descending (<). In Backward mode, it is exactly the opposite.
    /// </summary>
    private static bool ResolveOperator(SortDirection keyDirection, PaginationDirection pageDirection)
    {
        var greaterThanForForward = keyDirection == SortDirection.Ascending;
        return pageDirection == PaginationDirection.Forward ? greaterThanForForward : !greaterThanForForward;
    }

    private static Expression BuildEquality<T>(ParameterExpression parameter, SortSpecification<T> spec, CursorKeyValue keyValue)
    {
        var member = ExpressionHelper.BuildMemberAccess(parameter, spec.PropertyName);

        if (keyValue.Value is null)
            return Expression.Equal(member, Expression.Constant(null, member.Type));

        var underlyingType = Nullable.GetUnderlyingType(member.Type) ?? member.Type;
        var rawConstant = BuildTypedConstant(keyValue, underlyingType);
        Expression rightSide = member.Type != underlyingType
            ? Expression.Convert(rawConstant, member.Type)
            : rawConstant;

        return Expression.Equal(member, rightSide);
    }

    private static Expression BuildComparisonClause<T>(ParameterExpression parameter, SortSpecification<T> spec,
        CursorKeyValue keyValue, bool greaterThan)
    {
        var member = ExpressionHelper.BuildMemberAccess(parameter, spec.PropertyName);

        // The cursor value for this key is null: by convention "NULL is the smallest value"
        // (NULLS FIRST) - Not configurable behavior, but documented and predictable.
        if (keyValue.Value is null)
        {
            return greaterThan
                ? Expression.NotEqual(member, Expression.Constant(null, member.Type))
                : Expression.Constant(false); // Nothing is smaller than NULL
        }

        // We always make the constant with the Underlying type (non-nullable); BuildCompare itself
        // Convert it to Nullable<T> if needed or pass it directly to CompareTo(T).
        var underlyingType = Nullable.GetUnderlyingType(member.Type) ?? member.Type;
        var constant = BuildTypedConstant(keyValue, underlyingType);
        return ExpressionHelper.BuildCompare(member, constant, greaterThan);
    }

    private static Expression BuildTypedConstant(CursorKeyValue keyValue, Type underlyingTargetType)
    {
        var value = keyValue.Value!;

        if (!underlyingTargetType.IsInstanceOfType(value))
            value = Convert.ChangeType(value, underlyingTargetType, System.Globalization.CultureInfo.InvariantCulture);

        return Expression.Constant(value, underlyingTargetType);
    }
}
