using Bisa.Pagination.Abstractions;
using Bisa.Pagination.Abstractions.Enums;
using Bisa.Pagination.Core;
using Microsoft.EntityFrameworkCore;

namespace Bisa.Pagination.EFCore;

/// <summary>
/// Async version of pagination methods for IQueryable<T> supported by EF Core.
/// Uses EF Core's actual ToListAsync/CountAsync (not the behind-the-scenes Sync implementation).
/// </summary>
public static class EfPaginationExtensions
{
    extension<T>(IQueryable<T> source)
    {
        /// <summary>
        /// Async implementation of Offset paging. According to the requirement of "selecting Count calculation with the user",
        /// If request.CountMode == None, no Count query is executed (only 1 data query is executed),
        /// If it is Provided, no additional query will be executed (the user has given the value),
        /// And only in Compute mode, a separate COUNT(*) query is made to the database.
        /// </summary>
        public async Task<PageResult<T>> ToOffsetPageResultAsync(OffsetPageRequest request,
            CancellationToken cancellationToken = default)
        {
            var pagedQuery = source.ApplyOffsetPagination(request);
            var items = await pagedQuery.ToListAsync(cancellationToken).ConfigureAwait(false);

            var totalCount = request.CountMode switch
            {
                CountMode.None => null,
                CountMode.Provided => request.ProvidedTotalCount,
                CountMode.Compute => await source.LongCountAsync(cancellationToken).ConfigureAwait(false),
                _ => null
            };

            return PageResultBuilder.FromOffset(items, request, totalCount);
        }
         

        /// <summary>اجرای Async صفحه‌بندی کرسری با پشتیبانی کامل از Composite Key و Backforwarding.</summary>
        public async Task<PageResult<T>> ToCursorPageResultAsync(IReadOnlyList<SortSpecification<T>> sortSpecs,
            CursorPageRequest request,
            ICursorCodec cursorCodec,
            CancellationToken cancellationToken = default)
        {
            var plan = source.ApplyCursorPagination(sortSpecs, request, cursorCodec);
            var fetched = await plan.Query.ToListAsync(cancellationToken).ConfigureAwait(false);

            long? totalCount = request.CountMode switch
            {
                CountMode.None => null,
                CountMode.Provided => request.ProvidedTotalCount,
                CountMode.Compute => await source.LongCountAsync(cancellationToken).ConfigureAwait(false),
                _ => null
            };

            return PageResultBuilder.FromCursor(fetched, plan, cursorCodec, totalCount);
        }
    }
}
