namespace Bisa.Pagination.Abstractions;

/// <summary>
/// Common base for every pagination result, regardless of the strategy used to produce it.
/// </summary>
public abstract class PaginationResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public int PageSize { get; init; }

    public bool HasNext { get; init; }

    public bool HasPrevious { get; init; }
}
