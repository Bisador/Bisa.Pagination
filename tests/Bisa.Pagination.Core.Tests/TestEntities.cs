namespace Bisa.Pagination.Core.Tests;

public sealed class Article
{
    public int Id { get; init; }
    public DateTime? PublishedAt { get; init; }
    public string Title { get; set; } = "";
}

public static class TestData
{
    /// <summary>
    /// 10 items; Contains duplicate PublishedAt values (Duplicate Sort Values) and an item with PublishedAt=null (Null Values).
    /// The natural order (based on PublishedAt ASC then Id ASC) should be Id: 10,1,2,3,4,5,6,7,8,9
    /// Because Id=10 has the value of PublishedAt=null and according to the NULL convention, it is the smallest.
    /// </summary>
    public static List<Article> CreateArticles()
    {
        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return new List<Article>
        {
            new() { Id = 1, PublishedAt = baseDate.AddDays(1), Title = "A1" },
            new() { Id = 2, PublishedAt = baseDate.AddDays(1), Title = "A2" }, // Duplicate with Id=1
            new() { Id = 3, PublishedAt = baseDate.AddDays(2), Title = "A3" },
            new() { Id = 4, PublishedAt = baseDate.AddDays(3), Title = "A4" },
            new() { Id = 5, PublishedAt = baseDate.AddDays(3), Title = "A5" }, // Duplicate with Id=4
            new() { Id = 6, PublishedAt = baseDate.AddDays(4), Title = "A6" },
            new() { Id = 7, PublishedAt = baseDate.AddDays(5), Title = "A7" },
            new() { Id = 8, PublishedAt = baseDate.AddDays(6), Title = "A8" },
            new() { Id = 9, PublishedAt = baseDate.AddDays(7), Title = "A9" },
            new() { Id = 10, PublishedAt = null, Title = "A10-NoDate" }
        };
    }
}
