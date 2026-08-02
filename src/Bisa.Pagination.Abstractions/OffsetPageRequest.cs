using Bisa.Pagination.Abstractions.Enums;

namespace Bisa.Pagination.Abstractions;

/// <summary>
/// Classic page-number / page-size (skip/take) pagination request.
/// </summary>
public sealed class OffsetPageRequest : IPaginationRequest
{
    /// <summary>1-based page number.</summary>
    public int PageNumber { get; }

    public int PageSize { get;  }
   
    public CountMode CountMode { get; }
 
    public long? ProvidedTotalCount { get; }
    
    public int Skip => (PageNumber - 1) * PageSize;
    
    public OffsetPageRequest(int pageNumber, int pageSize, CountMode countMode = CountMode.Compute, long? providedTotalCount = null)
    {
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber),"Minimum page number must be 1");
        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize),"Page size must be at least 1");
        if (countMode == CountMode.Provided && providedTotalCount is null)
            throw new ArgumentException("When CountMode is on Provided, must pass providedTotalCount.", nameof(providedTotalCount));

        PageNumber = pageNumber;
        PageSize = pageSize;
        CountMode = countMode;
        ProvidedTotalCount = providedTotalCount;
    }

   
    public static OffsetPageRequest Create(
        int pageNumber, 
        int pageSize, 
        int maxPageSize = 200, 
        int defaultPageSize = 20, 
        CountMode countMode = CountMode.Compute,
        long? providedTotalCount = null)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = defaultPageSize;
        if (pageSize > maxPageSize) pageSize = maxPageSize;
        return new OffsetPageRequest(pageNumber, pageSize, countMode, providedTotalCount);
    }
}