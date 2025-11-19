using System;
using System.Linq;
using System.Threading.Tasks;
using EFCoreTest.Data;
using EFCoreTest.Extensions;
using EFCoreTest.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace EFCoreTest;

internal class Program
{
    static async Task Main(string[] args)
    {
            Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .WriteTo.File("Logs/CodingTest.log", rollingInterval: RollingInterval.Day)
        .CreateLogger();

        using var host = await Host.CreateDefaultBuilder(args).UseSerilog()
            .ConfigureServices((context, services) =>
            {
                services.AddLogging(builder => builder.AddConsole());

                services.AddDbContextPool<AppDbContext>((serviceProvider, options) =>
                {
                    // enable lazy-loading proxies
                    options.UseLazyLoadingProxies()
                           .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=EFCoreTestDb;Trusted_Connection=True;MultipleActiveResultSets=true");
                });

                // add coding test service for candidates to implement their EF Core code
                services.AddScoped<ICodingTestService, CodingTestService>();

            })
            .Build()
            .UseAsyncSeeding(async services =>
            {
                var logger = services.GetRequiredService<ILogger<Program>>();

                try
                {
                    var db = services.GetRequiredService<AppDbContext>();
                    await db.Database.MigrateAsync();

                    // Seed more data if necessary (create up to 60 posts with authors/comments)
                    var existingCount = await db.Posts.CountAsync();
                    if (existingCount < 60)
                    {
                        logger.LogInformation("Seeding sample data: existing posts = {Count}", existingCount);

                        // create a few users
                        var users = Enumerable.Range(1, 6).Select(i => new User { Name = $"User{i}" }).ToList();
                        db.Users.AddRange(users);

                        var posts = new System.Collections.Generic.List<Post>();
                        var rnd = new Random(123);
                        for (int i = 1; i <= 60; i++)
                        {
                            var author = users[rnd.Next(users.Count)];
                            var post = new Post
                            {
                                Title = $"Post {i} Title",
                                Content = $"This is the content of post {i}. Sample text to enable searching and testing.",
                                CreatedAt = DateTime.UtcNow.AddMinutes(-i),
                                Author = author
                            };
                            posts.Add(post);
                        }

                        db.Posts.AddRange(posts);

                        // add some comments
                        var comments = new System.Collections.Generic.List<Comment>();
                        for (int i = 0; i < posts.Count; i++)
                        {
                            var commentCount = rnd.Next(0, 4);
                            for (int c = 0; c < commentCount; c++)
                            {
                                var commenter = users[rnd.Next(users.Count)];
                                comments.Add(new Comment
                                {
                                    Content = $"Comment {c + 1} on post {i + 1}",
                                    CreatedAt = DateTime.UtcNow.AddMinutes(-(i + c)),
                                    Post = posts[i],
                                    Author = commenter
                                });
                            }
                        }

                        db.Comments.AddRange(comments);

                        await db.SaveChangesAsync();

                        logger.LogInformation("Seeded sample data: posts={Posts} users={Users} comments={Comments}", posts.Count, users.Count, comments.Count);
                    }

                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred seeding the DB.");
                    throw;
                }
            });

        // Example: resolve the coding test service and call methods to exercise them
        using (var scope = host.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<Program>>();
            var codingService = services.GetRequiredService<ICodingTestService>();

            // 1) Call the report generator
            try
            {
                logger.LogInformation("Calling GeneratePostSummaryReportAsync(50)");
                await codingService.GeneratePostSummaryReportAsync(50);
                logger.LogInformation("GeneratePostSummaryReportAsync completed successfully.");
            }
            catch (NotImplementedException)
            {
                logger.LogWarning("GeneratePostSummaryReportAsync is not implemented.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "GeneratePostSummaryReportAsync threw an exception.");
            }

            // 2) Call the simple search
            try
            {
                logger.LogInformation("Calling SearchPostSummariesAsync(\"post\", 10)");
                var results = await codingService.SearchPostSummariesAsync("post", 10);
                logger.LogInformation("SearchPostSummariesAsync returned {Count} items.", results?.Count ?? 0);
                if (results != null)
                {
                    foreach (var dto in results.Take(5))
                    {
                        logger.LogInformation("Search result: {Id} {Title} {Author} Comments={Count}", dto.Id, dto.Title, dto.AuthorName, dto.CommentCount);
                    }
                }
            }
            catch (NotImplementedException)
            {
                logger.LogWarning("SearchPostSummariesAsync is not implemented.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SearchPostSummariesAsync threw an exception.");
            }

            // 3) Call the paging search with ordering by CreatedAt
            try
            {
                logger.LogInformation("Calling SearchPostSummariesAsync with paging and ordering by CreatedAt");
                var paged = await codingService.SearchPostSummariesAsync("post", 10, 10, p => p.CreatedAt, true);
                logger.LogInformation("Paged search returned {Count} items.", paged?.Count ?? 0);
                if (paged != null)
                {
                    foreach (var dto in paged)
                    {
                        logger.LogInformation("Paged: {Id} {Title} {CreatedAt}", dto.Id, dto.Title, dto.CreatedAt);
                    }
                }
            }
            catch (NotImplementedException)
            {
                logger.LogWarning("Paged SearchPostSummariesAsync is not implemented.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Paged SearchPostSummariesAsync threw an exception.");
            }
        }

        // Graceful shutdown
        await host.StopAsync();
    }
}

