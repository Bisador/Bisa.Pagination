using Microsoft.EntityFrameworkCore;

namespace Bisa.Pagination.EFCore.Tests;

public sealed class Order
{
    public int Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public decimal Amount { get; init; }
}

public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
}

public static class TestDbContextFactory
{
    public static TestDbContext CreateWithSeedData(string dbName)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var context = new TestDbContext(options);
        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var i = 1; i <= 25; i++)
        {
            context.Orders.Add(new Order
            {
                Id = i,
                CreatedAt = baseDate.AddHours(i % 5), // Intentional duplicate values for Duplicate Sort Values
                Amount = i * 10m
            });
        }

        context.SaveChanges();
        return context;
    }
}