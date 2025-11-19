using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace EFCoreTest.Services;

/// <summary>
/// Service interface for the EF Core coding test. Implementations must perform EF Core
/// operations against `AppDbContext` and obey the documented requirements for each method.
/// </summary>
public interface ICodingTestService
{

    /// <summary>
    /// Generate a machine-parseable report describing posts and related information.
    ///
    /// Signature: `Task GeneratePostSummaryReportAsync(int maxItems)`
    ///
    /// Expected behavior (for candidate evaluation):
    /// - Emit a clearly delimited sequence of output lines describing up to `maxItems` posts.
    /// - Each output line must include PostId, AuthorName (blank if none), CommentCount, and
    ///   the AuthorName of the most recent comment (blank if none). The reviewer will parse
    ///   these lines to validate correctness and performance.
    /// - The method must be read-only and must handle missing related data gracefully.
    /// - Performance constraint: for typical small datasets (e.g., 50 posts), the implementation
    ///   should complete while issuing a small number of database commands; naive approaches
    ///   that generate one query per post are likely to fail grading.
    ///
    /// The method is intentionally named for the goal (report generation) rather than the
    /// specific technique the candidate should use. Do not change the signature.
    /// </summary>
    Task GeneratePostSummaryReportAsync(int maxItems);

    /// <summary>
    /// Search posts and return compact DTOs.
    ///
    /// Signature: `Task<IList<PostDto>> SearchPostSummariesAsync(string query, int maxResults = 50)`
    ///
    /// Expected behavior:
    /// - Return at most `maxResults` posts matching `query`.
    /// - If `query` is null, empty or whitespace, treat it as no filter and return unfiltered
    ///   results (up to `maxResults`).
    /// - Matching: `query` appears as a substring in Title OR Content (case-insensitive).
    /// - Results ordered by CreatedAt descending.
    /// - DTO contract: Id, Title, Excerpt (<=200 chars, append "..." if truncated), AuthorName,
    ///   CommentCount.
    /// - Non-functional: mapping should be efficient; avoid materializing full entity graphs
    ///   before projection. Implementations that fetch entities then map in-memory are likely
    ///   to be flagged by the grader.
    ///
    /// The signature is designed to guide candidates toward the required result shape while
    /// leaving the implementation strategy to them.
    /// </summary>
    Task<IList<PostDto>> SearchPostSummariesAsync(string query, int maxResults = 50);

    /// <summary>
    /// Paging/ordering/filtering variant. This signature explicitly exposes paging and ordering
    /// parameters to the candidate and tests. Implementations should accept these parameters
    /// and return the requested page efficiently.
    ///
    /// Signature: `Task<IList<PostDto>> SearchPostSummariesAsync<TKey>(string query, int skip, int take, Expression<Func<PostDto, TKey>> orderBySelector, bool descending)`
    ///
    /// - `query`: filter text; null/empty => treat as no filter (return unfiltered results up to take)
    /// - `skip`: number of records to skip (paging)
    /// - `take`: number of records to return
    /// - `orderBySelector`: an expression selecting the DTO member to order by. Use a generic
    ///   selector so the property type is preserved and can be translated to entity ordering.
    /// - `descending`: whether to sort descending
    ///
    /// Non-functional requirement: the implementation must execute the paging/filtering on the
    /// database (avoid client-side paging) and should result in a single query when possible.
    /// </summary>
    Task<IList<PostDto>> SearchPostSummariesAsync<TKey>(string query, int skip, int take, Expression<Func<PostDto, TKey>> orderBySelector, bool descending);
}
