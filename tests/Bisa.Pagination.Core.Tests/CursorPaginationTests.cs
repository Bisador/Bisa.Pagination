using Bisa.Pagination.Abstractions;
using Bisa.Pagination.Abstractions.Enums;
using Bisa.Pagination.Abstractions.Exceptions; 

namespace Bisa.Pagination.Core.Tests;

public class CursorPaginationTests
{
    private static readonly byte[] SigningKey = "unit-test-signing-key-32-bytes!!"u8.ToArray();

    private static List<SortSpecification<Article>> SortSpecs() =>
    [
        new(a => a.PublishedAt, SortDirection.Ascending),
        new(a => a.Id, SortDirection.Ascending)
    ];

    private static DefaultCursorCodec CreateCodec(TimeSpan? ttl = null) => new(new PaginationOptions
    {
        CursorProtection = CursorProtection.HashSigned,
        CursorSigningKey = SigningKey,
        CursorTimeToLive = ttl ?? TimeSpan.FromHours(1)
    });

    [Fact]
    public void FirstPage_ReturnsEarliestItems_NullsFirst_AndNoPreviousPage()
    {
        var query = TestData.CreateArticles().AsQueryable();
        var codec = CreateCodec();
        var request = new CursorPageRequest(cursor: null, pageSize: 3);

        var result = query.ToCursorPageResult(SortSpecs(), request, codec);

        Assert.Equal([10, 1, 2], result.Items.Select(a => a.Id));
        Assert.False(result.HasPreviousPage);
        Assert.True(result.HasNextPage);
        Assert.NotNull(result.NextCursor);
        Assert.Null(result.PreviousCursor);
    }

    [Fact]
    public void Forward_SecondPage_ContinuesCorrectlyAfterDuplicateSortValues()
    {
        var query = TestData.CreateArticles().AsQueryable();
        var codec = CreateCodec();
        var page1 = query.ToCursorPageResult(SortSpecs(), new CursorPageRequest(null, 3), codec);

        var page2 = query.ToCursorPageResult(SortSpecs(),
            new CursorPageRequest(page1.NextCursor, 3, PaginationDirection.Forward), codec);

        Assert.Equal([3, 4, 5], page2.Items.Select(a => a.Id));
        Assert.True(page2.HasPreviousPage);
        Assert.True(page2.HasNextPage);
    }

    [Fact]
    public void Forward_LastPage_ReturnsSingleRemainingItem_NoNextPage()
    {
        var query = TestData.CreateArticles().AsQueryable();
        var codec = CreateCodec();

        var page1 = query.ToCursorPageResult(SortSpecs(), new CursorPageRequest(null, 3), codec);
        var page2 = query.ToCursorPageResult(SortSpecs(), new CursorPageRequest(page1.NextCursor, 3), codec);
        var page3 = query.ToCursorPageResult(SortSpecs(), new CursorPageRequest(page2.NextCursor, 3), codec);
        var page4 = query.ToCursorPageResult(SortSpecs(), new CursorPageRequest(page3.NextCursor, 3), codec);

        // One Item + Last Page
        Assert.Single(page4.Items);
        Assert.Equal(9, page4.Items[0].Id);
        Assert.False(page4.HasNextPage);
        Assert.True(page4.HasPreviousPage);
    }

    [Fact]
    public void Backward_FromMiddlePage_ReturnsPreviousItemsInNaturalOrder()
    {
        var query = TestData.CreateArticles().AsQueryable();
        var codec = CreateCodec();

        var page1 = query.ToCursorPageResult(SortSpecs(), new CursorPageRequest(null, 3), codec);
        var page2 = query.ToCursorPageResult(SortSpecs(), new CursorPageRequest(page1.NextCursor, 3), codec);
        var page3 = query.ToCursorPageResult(SortSpecs(), new CursorPageRequest(page2.NextCursor, 3), codec); // 6,7,8

        var backToPage2 = query.ToCursorPageResult(SortSpecs(),
            new CursorPageRequest(page3.PreviousCursor, 3, PaginationDirection.Backward), codec);

        Assert.Equal([3, 4, 5], backToPage2.Items.Select(a => a.Id));
        Assert.True(backToPage2.HasNextPage);
        Assert.True(backToPage2.HasPreviousPage);
    }

    [Fact]
    public void EmptyResult_WhenPagingPastTheLastItem()
    {
        var query = TestData.CreateArticles().AsQueryable();
        var codec = CreateCodec();

        var lastArticle = TestData.CreateArticles().OrderBy(a => a.PublishedAt).ThenBy(a => a.Id).Last();
        var position = new CursorPosition([
            new CursorKeyValue("PublishedAt", lastArticle.PublishedAt, typeof(DateTime).FullName!),
            new CursorKeyValue("Id", lastArticle.Id, typeof(int).FullName!)
        ], DateTimeOffset.UtcNow);
        var cursor = codec.Encode(position);

        var result = query.ToCursorPageResult(SortSpecs(), new CursorPageRequest(cursor, 3), codec);

        Assert.Empty(result.Items);
        Assert.False(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public void InvalidCursor_ThrowsInvalidCursorException()
    {
        var query = TestData.CreateArticles().AsQueryable();
        var codec = CreateCodec();

        Assert.Throws<InvalidCursorException>(() =>
            query.ToCursorPageResult(SortSpecs(), new CursorPageRequest("این-یک-کرسر-معتبر-نیست", 3), codec));
    }

    [Fact]
    public void TamperedCursor_ThrowsTamperedCursorException()
    {
        var query = TestData.CreateArticles().AsQueryable();
        var codec = CreateCodec();

        var page1 = query.ToCursorPageResult(SortSpecs(), new CursorPageRequest(null, 3), codec);
        var parts = page1.NextCursor!.Split('.');
        // یک کاراکتر از بخش Payload را تغییر می‌دهیم تا امضا نامعتبر شود.
        var tamperedPayload = parts[0][..^1] + (parts[0][^1] == 'A' ? 'B' : 'A');
        var tamperedCursor = $"{tamperedPayload}.{parts[1]}";

        Assert.Throws<TamperedCursorException>(() =>
            query.ToCursorPageResult(SortSpecs(), new CursorPageRequest(tamperedCursor, 3), codec));
    }

    [Fact]
    public void ExpiredCursor_ThrowsExpiredCursorException()
    {
        var query = TestData.CreateArticles().AsQueryable();
        var shortLivedCodec = CreateCodec(TimeSpan.FromMilliseconds(1));

        var page1 = query.ToCursorPageResult(SortSpecs(), new CursorPageRequest(null, 3), shortLivedCodec);
        Thread.Sleep(30);

        Assert.Throws<ExpiredCursorException>(() =>
            query.ToCursorPageResult(SortSpecs(), new CursorPageRequest(page1.NextCursor, 3), shortLivedCodec));
    }

    [Fact]
    public void BackwardWithoutCursor_Throws()
    {
        var query = TestData.CreateArticles().AsQueryable();
        var codec = CreateCodec();

        Assert.Throws<ArgumentException>(() =>
            query.ToCursorPageResult(SortSpecs(), new CursorPageRequest(null, 3, PaginationDirection.Backward), codec));
    }
}
