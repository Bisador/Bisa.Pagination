namespace Bisa.Pagination.Abstractions;

/// <summary>
/// Classic page-number / page-size (skip/take) pagination request.
/// </summary>
public sealed class OffsetPaginationRequest : IPaginationRequest
{
    /// <summary>1-based page number.</summary>
    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public static OffsetPaginationRequest Create(int pageNumber, int pageSize, PaginationOptions? options = null)
    {
        options ??= new PaginationOptions();

        return new OffsetPaginationRequest
        {
            PageNumber = pageNumber < 1 ? 1 : pageNumber,
            PageSize = ClampPageSize(pageSize, options)
        };
    }

    private static int ClampPageSize(int pageSize, PaginationOptions options)
    {
        return pageSize <= 0 ? options.DefaultPageSize : Math.Min(pageSize, options.MaxPageSize);
    }
}