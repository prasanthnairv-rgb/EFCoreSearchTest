using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using EFCoreTest.Data;
using EFCoreTest.Services;

public class SearchTest
{
    private AppDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private CodingTestService GetService(AppDbContext db)
    {
        var logger = Mock.Of<ILogger<CodingTestService>>();
        return new CodingTestService(db, logger);
    }

    private async Task SeedData(AppDbContext db)
    {
        // Clear existing data
        db.Posts.RemoveRange(db.Posts);
        db.Users.RemoveRange(db.Users);
        await db.SaveChangesAsync();

        // Seed a single user
        var user = new User { Id = 1, Name = "Prasanth" };
        db.Users.Add(user);

        // Seed posts
        db.Posts.AddRange(
            new Post
            {
                Id = 1,
                Title = "Core Test",     
                Content = "This is a search content",
                AuthorId = 1,
                Author = user,
                CreatedAt = DateTime.Now.AddMinutes(-1)
            },
            new Post
            {
                Id = 2,
                Title = "Testing Post",            
                Content = "Content for unit tests",
                AuthorId = 1,
                Author = user,
                CreatedAt = DateTime.Now.AddMinutes(-2)
            },
            new Post
            {
                Id = 3,
                Title = "Random Post Test",        
                Content = "Search keyword",
                AuthorId = 1,
                Author = user,
                CreatedAt = DateTime.Now.AddMinutes(-3)
            },
            new Post
            {
                Id = 4,
                Title = "Insensitive Test",        
                Content = "Testing case insensitivity",
                AuthorId = 1,
                Author = user,
                CreatedAt = DateTime.Now.AddMinutes(-4)
            },
            new Post
            {
                Id = 5,
                Title = "Special! Characters Post",
                Content = "Content with special characters like !@#$",
                AuthorId = 1,
                Author = user,
                CreatedAt = DateTime.Now.AddMinutes(-5)
            }
        );

        await db.SaveChangesAsync();
    }


    // TEST 1: Title search
    [Fact]
    public async Task Search_By_Title_Returns_Correct_Result()
    {
        var db = GetDbContext();
        await SeedData(db);

        var service = GetService(db);

        var results = await service.SearchPostSummariesAsync("Core Test", 10);

        Assert.Single(results);
        Assert.Equal("Core Test", results.First().Title);
    }

    // TEST 2: Content search
    [Fact]
    public async Task Search_By_Content_Returns_Correct_Posts()
    {
        var db = GetDbContext();
        await SeedData(db);

        var service = GetService(db);

        var results = await service.SearchPostSummariesAsync("unit tests", 10);

        Assert.Single(results);
        Assert.Equal("Testing Post", results.First().Title);
    }

    // TEST 3: Max limit
    [Fact]
    public async Task Search_Returns_Limited_MaxItems()
    {
        var db = GetDbContext();
        await SeedData(db);

        var service = GetService(db);

        var results = await service.SearchPostSummariesAsync("", 2);

        Assert.Equal(2, results.Count);
    }

    // TEST 4: Empty list
    [Fact]
    public async Task Search_No_Match_Returns_Empty()
    {
        var db = GetDbContext();
        await SeedData(db);

        var service = GetService(db);

        var results = await service.SearchPostSummariesAsync("notfound", 10);

        Assert.Empty(results);
    }

    // TEST 5: Case insensitivity
    [Fact]
    public async Task Search_Is_Case_Insensitive()
    {
        var db = GetDbContext();
        await SeedData(db);

        var service = GetService(db);

        var results = await service.SearchPostSummariesAsync("insensitive test", 10);

        Assert.Contains(results, r => r.Title.Contains("Insensitive Test"));
    }

    //TEST 9: Date sort
    [Fact]
    public async Task Search_Results_Are_Sorted_By_CreatedAt_Desc()
    {
        var db = GetDbContext();
        await SeedData(db);

        var service = GetService(db);

        var results = await service.SearchPostSummariesAsync("", 10);

        Assert.True(results[0].CreatedAt > results[1].CreatedAt);
        Assert.True(results[1].CreatedAt > results[2].CreatedAt);
    }

    //TEST 10: Special characters
    [Fact]
    public async Task Search_With_Special_Characters()
    {
        var db = GetDbContext();
        await SeedData(db);

        var service = GetService(db);

        var results = await service.SearchPostSummariesAsync("content!", 10);

        Assert.Empty(results);
    }

    //TEST 11: Empty search
    [Fact]
    public async Task Search_NullOrEmpty_Query_Returns_All_Limited()
    {
        var db = GetDbContext();
        await SeedData(db);

        var service = GetService(db);

        var results1 = await service.SearchPostSummariesAsync("", 10);
        var results2 = await service.SearchPostSummariesAsync(null, 10);

        Assert.Equal(5, results1.Count);
        Assert.Equal(5, results2.Count);
    }

    //TEST 12: Large Data set
    [Fact]
    public async Task Search_Large_Dataset_Performance()
    {
        var db = GetDbContext();

        var user = new User { Id = 1, Name = "Prasanth" };
        db.Users.Add(user);

        for (int i = 0; i < 1000; i++)
        {
            db.Posts.Add(new Post
            {
                Title = $"Title {i}",
                Content = $"Content {i}",
                AuthorId = 1,
                Author = user,
                CreatedAt = DateTime.Now
            });
        }
        await db.SaveChangesAsync();

        var service = GetService(db);

        var results = await service.SearchPostSummariesAsync("Title 500", 10);

        Assert.Single(results);
        Assert.Equal("Title 500", results.First().Title);
    }
}
