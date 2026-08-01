namespace Bisa.Pagination.Abstractions;

public sealed class OffsetPaginationResult<T> : PaginationResult<T>
{
    public int PageNumber { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages { get; init; }
}
