using Bisa.Pagination.Abstractions;
using Bisa.Pagination.Abstractions.Enums; 

namespace Bisa.Pagination.Core.Tests;

public class OffsetPaginationTests
{
    private static IQueryable<Article> OrderedQuery() =>
        TestData.CreateArticles().AsQueryable().OrderBy(a => a.PublishedAt).ThenBy(a => a.Id);

    [Fact]
    public void FirstPage_ReturnsCorrectItemsAndMetadata()
    {
        var result = OrderedQuery().ToOffsetPageResult(new OffsetPageRequest(1, 3));

        Assert.Equal([10, 1, 2], result.Items.Select(a => a.Id));
        Assert.Equal(10, result.TotalCount);
        Assert.False(result.HasPreviousPage);
        Assert.True(result.HasNextPage);
        Assert.Equal(4, result.TotalPages);
    }

    [Fact]
    public void LastPage_MayHaveFewerItems_AndNoNextPage()
    {
        var result = OrderedQuery().ToOffsetPageResult(new OffsetPageRequest(4, 3));

        Assert.Single(result.Items); // آیتم دهم، صفحه آخر
        Assert.False(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
    }

    [Fact]
    public void EmptyResult_WhenPageNumberBeyondData()
    {
        var result = OrderedQuery().ToOffsetPageResult(new OffsetPageRequest(99, 3));

        Assert.Empty(result.Items);
        Assert.Equal(10, result.TotalCount);
        Assert.False(result.HasNextPage);
    }

    [Fact]
    public void CountMode_None_LeavesTotalCountNull()
    {
        var result = OrderedQuery().ToOffsetPageResult(new OffsetPageRequest(1, 3, CountMode.None));

        Assert.Null(result.TotalCount);
        Assert.Null(result.TotalPages);
    }

    [Fact]
    public void CountMode_Provided_UsesGivenValueWithoutRecomputing()
    {
        var result = OrderedQuery().ToOffsetPageResult(new OffsetPageRequest(1, 3, CountMode.Provided, providedTotalCount: 1000));

        Assert.Equal(1000, result.TotalCount);
    }

    [Fact]
    public void PageSize_IsClampedToMaxPageSize_WhenUsingFactoryMethod()
    {
        var request = OffsetPageRequest.Create(pageNumber: 1, pageSize: 10_000, maxPageSize: 50);

        Assert.Equal(50, request.PageSize);
    }
}
