#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EFCoreTest.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EFCoreTest.Services;

public class CodingTestService(AppDbContext db, ILogger<CodingTestService> logger) : ICodingTestService
{
    private readonly AppDbContext _db = db ?? throw new ArgumentNullException(nameof(db));
    private readonly ILogger<CodingTestService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    // Reusable projection
    private static readonly Expression<Func<Post, PostDto>> PostToDto = p => new PostDto
    {
        Id = p.Id,
        Title = p.Title ?? string.Empty,
        Excerpt = p.Content != null
            ? (p.Content.Length <= 200 ? p.Content : p.Content.Substring(0, 200) + "...")
            : string.Empty,
        AuthorName = p.Author != null ? (p.Author.Name ?? "Unknown") : "Unknown",
        CommentCount = p.Comments.Count,
        CreatedAt = p.CreatedAt
    };

    public async Task GeneratePostSummaryReportAsync(int maxItems)
    {
        _logger.LogInformation("REPORT_START");

        // If nothing
        if (maxItems <= 0)
        {
            _logger.LogInformation("REPORT_END (no items requested)");
            return;
        }

        try
        {
            // Server-side
            var query = _db.Posts
                .AsNoTracking()
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id,
                    AuthorName = p.Author != null ? (p.Author.Name ?? "Unknown") : "Unknown",
                    CommentCount = p.Comments.Count,
                    LatestCommentAuthor = p.Comments
                        .OrderByDescending(c => c.CreatedAt)
                        .Select(c => c.Author != null ? (c.Author.Name ?? "Unknown") : "Unknown")
                        .FirstOrDefault() ?? "None"
                })
                .Take(maxItems)
                .AsAsyncEnumerable();

            await foreach (var item in query.ConfigureAwait(false))
            {
                _logger.LogInformation(
                    "POST_SUMMARY|{Id}|{Author}|{Count}|{Latest}",
                    item.Id,
                    item.AuthorName,
                    item.CommentCount,
                    item.LatestCommentAuthor);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GeneratePostSummaryReportAsync failed");
            throw;
        }
        finally
        {
            _logger.LogInformation("REPORT_END");
        }
    }

    public async Task<IList<PostDto>> SearchPostSummariesAsync(string query, int maxResults = 50)
    {
        // Validation
        if (maxResults <= 0) return Array.Empty<PostDto>();

        try
        {
            IQueryable<Post> posts = _db.Posts.AsNoTracking();

            // Server-side case-insensitive
            if (!string.IsNullOrWhiteSpace(query))
            {
                // Trim whitespace & use pattern
                var pattern = $"%{query.Trim()}%";
                posts = posts.Where(p =>
                    EF.Functions.Like(p.Title, pattern) ||
                    EF.Functions.Like(p.Content, pattern));
            }

            // limited results.
            var list = await posts
                .OrderByDescending(p => p.CreatedAt)
                .ThenBy(p => p.Id)
                .Select(PostToDto)
                .Take(maxResults)
                .ToListAsync()
                .ConfigureAwait(false);

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearchPostSummariesAsync failed (query='{Query}', maxResults={Max})", query ?? "<null>", maxResults);
            throw;
        }
    }

    public async Task<IList<PostDto>> SearchPostSummariesAsync<TKey>(
    string query,
    int skip,
    int take,
    Expression<Func<PostDto, TKey>> orderBySelector,
    bool descending)
    {
        if (take <= 0)
            return Array.Empty<PostDto>();

        if (skip < 0)
            skip = 0;

        try
        {
            IQueryable<Post> posts = _db.Posts.AsNoTracking();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(query))
            {
                var pattern = $"%{query.Trim()}%";

                posts = posts.Where(p =>
                    EF.Functions.Like(p.Title, pattern) ||
                    EF.Functions.Like(p.Content, pattern));
            }

            // Base projection
            var dtoQuery = posts.Select(PostToDto);

            // Extract property name (supports UnaryExpression)
            var memberExpr =
                orderBySelector.Body is UnaryExpression u
                    ? u.Operand as MemberExpression
                    : orderBySelector.Body as MemberExpression;

            var memberName = memberExpr?.Member?.Name
                ?? throw new NotSupportedException("Only simple PostDto property ordering is supported.");

            // IMPORTANT: must be IOrderedQueryable<T> for ThenBy()
            IOrderedQueryable<PostDto> orderedQuery = memberName switch
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

            // Stable secondary ordering (critical for paging)
            orderedQuery = orderedQuery.ThenBy(x => x.Id);

            // Apply paging
            return await orderedQuery
                .Skip(skip)
                .Take(take)
                .ToListAsync()
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "SearchPostSummariesAsync (paged) failed (query='{Query}', skip={Skip}, take={Take})",
                query ?? "<null>", skip, take);

            throw;
        }
    }
}

