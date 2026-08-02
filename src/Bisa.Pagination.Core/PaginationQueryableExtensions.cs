namespace Bisa.Pagination.Core;

/// <summary>
/// Core's main entry point: extension methods on IQueryable<T> that work independently of EF/Dapper
/// (For EF Core use Bisa.Pagination.EF which adds Async versions of these methods).
/// All these methods are Deferred and do not execute a return query; That is, according to need
/// "Pagination is part of the Expression", the user can continue the result with Select/Include/...
/// </summary>
public static class PaginationQueryableExtensions
{
    extension<T>(IQueryable<T> source)
    {
        /// <summary>اعمال Offset Pagination. حتماً قبل از این متد یک OrderBy پایدار اعمال کرده باشید.</summary>
        public IQueryable<T> ApplyOffsetPagination(OffsetPageRequest request) =>
            OffsetPaginator.BuildQuery(source, request);

        /// <summary>
        /// Apply Cursor/Keyset Pagination with Composite Key. Sorting by this method itself
        /// (based on sortSpecs) applies; No need for manual OrderBy before that
        /// (And no other OrderBy should be applied before it because it will be overwritten).
        /// </summary>
        public CursorQueryPlan<T> ApplyCursorPagination(IReadOnlyList<SortSpecification<T>> sortSpecs,
            CursorPageRequest request,
            ICursorCodec cursorCodec) =>
            CursorPaginator.BuildQuery(source, sortSpecs, request, cursorCodec);

        /// <summary>
        /// Synchronous implementation (Sync) of Offset pagination - for Providers that do not have true Async
        /// (like LINQ-to-Objects) or test scenarios. For EF Core, use the Async version of Bisa.Pagination.EF.
        /// </summary>
        public PageResult<T> ToOffsetPageResult(OffsetPageRequest request)
        {
            var items = source.ApplyOffsetPagination(request).ToList();
            var totalCount = request.CountMode switch
            {
                CountMode.None => null,
                CountMode.Provided => request.ProvidedTotalCount,
                CountMode.Compute => source.LongCount(),
                _ => null
            };
            return PageResultBuilder.FromOffset(items, request, totalCount);
        }

        /// <summary>نسخه Sync اجرای صفحه‌بندی کرسری (برای LINQ-to-Objects/تست؛ برای EF Core از Bisa.Pagination.EF استفاده کنید).</summary>
        public PageResult<T> ToCursorPageResult(IReadOnlyList<SortSpecification<T>> sortSpecs,
            CursorPageRequest request,
            ICursorCodec cursorCodec)
        {
            var plan = source.ApplyCursorPagination(sortSpecs, request, cursorCodec);
            var fetched = plan.Query.ToList();

            long? totalCount = request.CountMode switch
            {
                CountMode.None => null,
                CountMode.Provided => request.ProvidedTotalCount,
                CountMode.Compute => source.LongCount(),
                _ => null
            };

            return PageResultBuilder.FromCursor(fetched, plan, cursorCodec, totalCount);
        }
    }
}
