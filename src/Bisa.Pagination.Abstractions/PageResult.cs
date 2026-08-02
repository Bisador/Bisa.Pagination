namespace Bisa.Pagination.Abstractions;

/// <summary>
/// The final result of pagination result despite of offset or cursor or hybrid method
/// irrelevant properties in each method set null by default 
/// </summary>
public sealed class PageResult<T>(IReadOnlyList<T> items)
{
    public IReadOnlyList<T> Items { get; } = items ?? throw new ArgumentNullException(nameof(items));
 
    public long? TotalCount { get; init; }

    public bool HasNextPage { get; init; }

    public bool HasPreviousPage { get; init; }

    
    public string? NextCursor { get; init; }
 
    public string? PreviousCursor { get; init; }
 
    public int? PageNumber { get; init; }
 
    public int PageSize { get; init; }
 
    public int? TotalPages => TotalCount.HasValue && PageSize > 0
        ? (int)Math.Ceiling(TotalCount.Value / (double)PageSize)
        : null;

    public static PageResult<T> Empty(int pageSize) => new(Array.Empty<T>())
    {
        TotalCount = 0,
        HasNextPage = false,
        HasPreviousPage = false,
        PageSize = pageSize
    };
}
