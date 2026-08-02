using Bisa.Pagination.Abstractions;
using Bisa.Pagination.Abstractions.Enums;
using Bisa.Pagination.Core;

namespace Bisa.Pagination.EFCore.Tests;

public class EfPaginationTests
{
    private static DefaultCursorCodec CreateCodec() => new(new PaginationOptions
    {
        CursorProtection = CursorProtection.HashSigned,
        CursorSigningKey = "ef-tests-signing-key-32-bytes!!!"u8.ToArray(),
        CursorTimeToLive = TimeSpan.FromHours(1)
    });

    private static List<SortSpecification<Order>> SortSpecs() =>
    [
        new(o => o.CreatedAt, SortDirection.Ascending),
        new(o => o.Id, SortDirection.Ascending)
    ];

    [Fact]
    public async Task OffsetAsync_ReturnsCorrectPage_WithComputedCount()
    {
        await using var db = TestDbContextFactory.CreateWithSeedData(nameof(OffsetAsync_ReturnsCorrectPage_WithComputedCount));

        var result = await db.Orders
            .OrderBy(o => o.CreatedAt).ThenBy(o => o.Id)
            .ToOffsetPageResultAsync(new OffsetPageRequest(2, 10));

        Assert.Equal(10, result.Items.Count);
        Assert.Equal(25, result.TotalCount);
        Assert.True(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
    }

    [Fact]
    public async Task OffsetAsync_CountModeNone_DoesNotComputeTotalCount()
    {
        await using var db = TestDbContextFactory.CreateWithSeedData(nameof(OffsetAsync_CountModeNone_DoesNotComputeTotalCount));

        var result = await db.Orders
            .OrderBy(o => o.CreatedAt).ThenBy(o => o.Id)
            .ToOffsetPageResultAsync(new OffsetPageRequest(1, 10, CountMode.None));

        Assert.Null(result.TotalCount);
    }

    [Fact]
    public async Task CursorAsync_ForwardThenBackward_ReturnsConsistentItems()
    {
        await using var db = TestDbContextFactory.CreateWithSeedData(nameof(CursorAsync_ForwardThenBackward_ReturnsConsistentItems));
        var codec = CreateCodec();

        var page1 = await db.Orders.ToCursorPageResultAsync(SortSpecs(), new CursorPageRequest(null, 10), codec);
        var page2 = await db.Orders.ToCursorPageResultAsync(SortSpecs(),
            new CursorPageRequest(page1.NextCursor, 10, PaginationDirection.Forward), codec);
        var backToPage1 = await db.Orders.ToCursorPageResultAsync(SortSpecs(),
            new CursorPageRequest(page2.PreviousCursor, 10, PaginationDirection.Backward), codec);

        Assert.Equal(page1.Items.Select(o => o.Id), backToPage1.Items.Select(o => o.Id));
    }

    [Fact]
    public async Task CursorAsync_LastPage_HasNoNextPage()
    {
        await using var db = TestDbContextFactory.CreateWithSeedData(nameof(CursorAsync_LastPage_HasNoNextPage));
        var codec = CreateCodec();

        var page1 = await db.Orders.ToCursorPageResultAsync(SortSpecs(), new CursorPageRequest(null, 10), codec);
        var page2 = await db.Orders.ToCursorPageResultAsync(SortSpecs(),
            new CursorPageRequest(page1.NextCursor, 10), codec);
        var page3 = await db.Orders.ToCursorPageResultAsync(SortSpecs(),
            new CursorPageRequest(page2.NextCursor, 10), codec); // 25 items total => آخرین صفحه ۵ آیتم

        Assert.Equal(5, page3.Items.Count);
        Assert.False(page3.HasNextPage);
    }
}
