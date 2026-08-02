namespace Bisa.Pagination.Core;

/// <summary>
/// A typed sort key for the <typeparamref name="T"/> entity.
/// An ordered list of this class Composite Key builds the cursor pagination;
/// Example: [ new(x => x.CreatedAt, Descending), new(x => x.Id, Ascending) ]
/// (The last key should usually be unique to avoid Duplicate Sort Values).
/// </summary>
public sealed class SortSpecification<T>
{
    public Expression<Func<T, object?>> KeySelector { get; }
    public SortDirection Direction { get; }
    public string PropertyName { get; }
    public Type PropertyType { get; }

    public SortSpecification(Expression<Func<T, object?>> keySelector, SortDirection direction = SortDirection.Ascending)
    {
        KeySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        Direction = direction;
        var (name, type) = ExpressionHelper.GetMember(keySelector);
        PropertyName = name;
        PropertyType = type;
    }

    internal SortSpecification<T> WithDirection(SortDirection direction) =>
        new(KeySelector, direction);

    public SortField ToSortField() => new(PropertyName, Direction);
}
