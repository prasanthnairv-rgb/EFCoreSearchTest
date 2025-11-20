using EFCoreTest.Data;
using EFCoreTest.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading.Tasks;

public class CodingTestServiceTests
{
    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        return new AppDbContext(options);
    }

    [Xunit.Fact]
    public async Task SearchPostSummariesAsync_ReturnsResults()
    {
        var db = CreateDbContext();

        db.Posts.Add(new Post
        {
            Title = "Test Post",
            Content = "Hello world",
            CreatedAt = DateTime.UtcNow,
            Author = new User { Name = "User1" }
        });

        await db.SaveChangesAsync();

        var service = new CodingTestService(db, NullLogger<CodingTestService>.Instance);

        var results = await service.SearchPostSummariesAsync("test", 10);

        Xunit.Assert.Single(results);
        Xunit.Assert.Equal("Test Post", results[0].Title);
    }
}
