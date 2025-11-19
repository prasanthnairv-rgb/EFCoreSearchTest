using System.Collections.Generic;

namespace EFCoreTest.Data;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;

    // virtual for EF Core lazy-loading proxies
    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
}
