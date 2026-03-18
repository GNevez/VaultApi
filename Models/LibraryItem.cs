using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace vaultApi.Models;

public class LibraryItem
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [ForeignKey("UserId")]
    public User User { get; set; } = null!;

    [Required]
    public int SourceId { get; set; }

    [ForeignKey("SourceId")]
    public Source Source { get; set; } = null!;

    [Required]
    public int GameIndex { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
