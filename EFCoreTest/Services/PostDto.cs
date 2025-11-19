using System;

namespace EFCoreTest.Services;

public class PostDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Excerpt { get; set; }
    public string? AuthorName { get; set; }
    public int CommentCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
