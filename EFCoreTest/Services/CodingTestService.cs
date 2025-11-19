using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EFCoreTest.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EFCoreTest.Services;

public class CodingTestService(AppDbContext db, ILogger<CodingTestService> logger) : ICodingTestService
{
    private readonly AppDbContext _db = db;
    private readonly ILogger<CodingTestService> _logger = logger;

    public async Task GeneratePostSummaryReportAsync(int maxItems)
    {
        // Task placeholder:
        // - Emit REPORT_START, then up to `maxItems` lines prefixed with "POST_SUMMARY|" and
        //   finally REPORT_END. Each summary line must include PostId|AuthorName|CommentCount|LatestCommentAuthor.
        // - Method must be read-only and efficient for large datasets;
        // Implement the method body in the assessment; do not change the signature.

        try 
        { 
        _logger.LogInformation("REPORT_START");

        var query =
            _db.Posts
               .AsNoTracking()
               .OrderByDescending(p => p.CreatedAt)
               .Select(p => new
               {
                   p.Id,
                   AuthorName = p.Author.Name,
                   CommentCount = p.Comments.Count,
                   LatestCommentAuthor =
                    p.Comments
                     .OrderByDescending(c => c.CreatedAt)
                     .Select(c => c.Author.Name)
                     .FirstOrDefault()
               })
               .Take(maxItems)
               .AsAsyncEnumerable();

        await foreach (var item in query)
        {
            _logger.LogInformation(
                "POST_SUMMARY|{Id}|{Author}|{Count}|{Latest}",
                item.Id,
                item.AuthorName,
                item.CommentCount,
                item.LatestCommentAuthor ?? ""
            );
        }

        // End report
        _logger.LogInformation("REPORT_END");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GeneratePostSummaryReportAsync failed");

            throw new NotImplementedException(
                "Implement GeneratePostSummaryReportAsync according to assessment requirements."
            );
        }
    }

    public async Task<IList<PostDto>> SearchPostSummariesAsync(string query, int maxResults = 50)
    {
        // Task placeholder:
        // - Return at most `maxResults` PostDto entries.
        // - Treat null/empty/whitespace query as no filter (return unfiltered results up to maxResults).
        // - Matching: case-insensitive substring in Title OR Content.
        // - Order by CreatedAt descending, project to PostDto, and avoid materializing full entities.
        // Implement the method body in the assessment; do not change the signature.
        try
        {
            IQueryable<Post> q = _db.Posts.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(query))
            {
                var lowered = query.ToLower();

                q = q.Where(p =>
                    p.Title.ToLower().Contains(lowered) ||
                    p.Content.ToLower().Contains(lowered)
                );
            }

            return await q
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PostDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    AuthorName = p.Author.Name,
                    CommentCount = p.Comments.Count,
                    CreatedAt = p.CreatedAt
                })
                .Take(maxResults)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearchPostSummariesAsync failed");

            throw new NotImplementedException("Implement SearchPostSummariesAsync according to assessment requirements.");
        }
    }

    public async Task<IList<PostDto>> SearchPostSummariesAsync<TKey>(string query,int skip,int take,Expression<Func<PostDto, TKey>> orderBySelector,bool descending)
    {
        // Task placeholder: 
        // - Server-side filter by query (null/empty => no filter), server-side ordering based on 
        // the provided DTO selector, then Skip/Take for paging. Project to PostDto and avoid 
        // per-row queries or client-side paging. 
        // - Implementations may choose which selectors to support; unsupported selectors may 
        // be rejected by the grader. 
        // Implement the method body in the assessment; do not change the signature.
        try
        {
            IQueryable<Post> q = _db.Posts.AsNoTracking();

            // Filter
            if (!string.IsNullOrWhiteSpace(query))
            {
                var lowered = query.ToLower();
                q = q.Where(p =>
                    p.Title.ToLower().Contains(lowered) ||
                    p.Content.ToLower().Contains(lowered)
                );
            }

            // DTO
            var dtoQuery = q.Select(p => new PostDto
            {
                Id = p.Id,
                Title = p.Title,
                AuthorName = p.Author.Name,
                CommentCount = p.Comments.Count,
                CreatedAt = p.CreatedAt
            });

            // Sort
            string memberName = (orderBySelector.Body as MemberExpression)?.Member?.Name;

            if (memberName is null)
                throw new NotSupportedException("Only simple PostDto property ordering is supported.");

            dtoQuery = memberName switch
            {
                nameof(PostDto.CreatedAt) =>
                    descending ? dtoQuery.OrderByDescending(x => x.CreatedAt)
                               : dtoQuery.OrderBy(x => x.CreatedAt),

                nameof(PostDto.Title) =>
                    descending ? dtoQuery.OrderByDescending(x => x.Title)
                               : dtoQuery.OrderBy(x => x.Title),

                nameof(PostDto.CommentCount) =>
                    descending ? dtoQuery.OrderByDescending(x => x.CommentCount)
                               : dtoQuery.OrderBy(x => x.CommentCount),

                nameof(PostDto.Id) =>
                    descending ? dtoQuery.OrderByDescending(x => x.Id)
                               : dtoQuery.OrderBy(x => x.Id),

                _ => throw new NotSupportedException($"Ordering by '{memberName}' is not supported.")
            };

            // Paging
            return await dtoQuery
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paged SearchPostSummariesAsync failed");

            throw new NotImplementedException("Implement SearchPostSummariesAsync (paged) according to assessment requirements.");
        }
    }

}