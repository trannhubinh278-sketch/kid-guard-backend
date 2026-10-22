using System.ComponentModel.DataAnnotations;

namespace NhuBinh_2921.Models;

public class Post
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string UserName { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Image { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
