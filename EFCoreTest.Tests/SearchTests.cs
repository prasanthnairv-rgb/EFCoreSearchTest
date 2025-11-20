#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using EFCoreTest.Data;
using EFCoreTest.Services;

public sealed class SearchTest : IDisposable
{
    private readonly DateTime _baseTime = new(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    private readonly AppDbContext _db;

    public SearchTest()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging(false)
            .EnableDetailedErrors()
            .Options;

        _db = new AppDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private CodingTestService CreateService() =>
        new(_db, Mock.Of<ILogger<CodingTestService>>());

    private async Task SeedStandardDataAsync()
    {
        _db.Posts.RemoveRange(_db.Posts);
        _db.Users.RemoveRange(_db.Users);
        await _db.SaveChangesAsync();

        var user = new User { Id = 1, Name = "Prasanth" };
        _db.Users.Add(user);

        _db.Posts.AddRange(
            new Post
            {
                Id = 1,
                Title = "Core Test",
                Content = "This is a search content",
                Author = user,
                AuthorId = 1,
                CreatedAt = _baseTime.AddMinutes(-1)
            },
            new Post
            {
                Id = 2,
                Title = "Testing Post",
                Content = "Content for unit tests",
                Author = user,
                AuthorId = 1,
                CreatedAt = _baseTime.AddMinutes(-2)
            },
            new Post
            {
                Id = 3,
                Title = "Random Post Test",
                Content = "Search keyword",
                Author = user,
                AuthorId = 1,
                CreatedAt = _baseTime.AddMinutes(-3)
            },
            new Post
            {
                Id = 4,
                Title = "Insensitive Test",
                Content = "Testing case insensitivity",
                Author = user,
                AuthorId = 1,
                CreatedAt = _baseTime.AddMinutes(-4)
            },
            new Post
            {
                Id = 5,
                Title = "Special! Characters Post",
                Content = "Content with special characters like !@#$",
                Author = user,
                AuthorId = 1,
                CreatedAt = _baseTime.AddMinutes(-5)
            }
        );

        await _db.SaveChangesAsync();
    }

    //Test Cases

    [Fact]
    public async Task Search_By_Title_Returns_Correct_Result()
    {
        await SeedStandardDataAsync();
        var service = CreateService();

        var results = await service.SearchPostSummariesAsync("Core Test", 10);

        Assert.Single(results);
        Assert.Equal("Core Test", results[0].Title);
    }

    [Fact]
    public async Task Search_By_Content_Returns_Correct_Posts()
    {
        await SeedStandardDataAsync();
        var service = CreateService();

        var results = await service.SearchPostSummariesAsync("unit tests", 10);

        Assert.Single(results);
        Assert.Equal("Testing Post", results[0].Title);
    }

    [Fact]
    public async Task Search_Returns_Limited_MaxItems()
    {
        await SeedStandardDataAsync();
        var service = CreateService();

        var results = await service.SearchPostSummariesAsync("", 2);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task Search_No_Match_Returns_Empty()
    {
        await SeedStandardDataAsync();
        var service = CreateService();

        var results = await service.SearchPostSummariesAsync("notfound", 10);

        Assert.Empty(results);
    }

    [Fact]
    public async Task Search_Is_Case_Insensitive()
    {
        await SeedStandardDataAsync();
        var service = CreateService();

        var results = await service.SearchPostSummariesAsync("insensitive test", 10);

        Assert.Contains(results, r => r.Title == "Insensitive Test");
    }

    [Fact]
    public async Task Search_Results_Are_Sorted_By_CreatedAt_Desc()
    {
        await SeedStandardDataAsync();
        var service = CreateService();

        var results = await service.SearchPostSummariesAsync("", 10);

        Assert.True(results[0].CreatedAt > results[1].CreatedAt);
        Assert.True(results[1].CreatedAt > results[2].CreatedAt);
    }

    [Fact]
    public async Task Search_With_Special_Characters_Returns_Zero()
    {
        await SeedStandardDataAsync();
        var service = CreateService();

        var results = await service.SearchPostSummariesAsync("content!", 10);

        Assert.Empty(results);
    }

    [Fact]
    public async Task Search_NullOrEmpty_Query_Returns_All()
    {
        await SeedStandardDataAsync();
        var service = CreateService();

        var emptyResults = await service.SearchPostSummariesAsync("", 10);

        // Intentional null
        string? nullQuery = null;
        var nullResults = await service.SearchPostSummariesAsync(nullQuery!, 10);

        Assert.Equal(5, emptyResults.Count);
        Assert.Equal(5, nullResults.Count);
    }

    [Fact]
    public async Task Search_Large_Dataset_Performance()
    {
        var user = new User { Name = "Prasanth" };
        _db.Users.Add(user);

        for (var i = 0; i < 1000; i++)
        {
            _db.Posts.Add(
                new Post
                {
                    Title = $"Title {i}",
                    Content = $"Content {i}",
                    Author = user,
                    AuthorId = user.Id,
                    CreatedAt = _baseTime
                });
        }
        await _db.SaveChangesAsync();

        var service = CreateService();

        var results = await service.SearchPostSummariesAsync("Title 500", 10);

        Assert.Single(results);
        Assert.Equal("Title 500", results[0].Title);
    }
}
