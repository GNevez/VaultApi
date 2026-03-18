using Microsoft.EntityFrameworkCore;
using vaultApi.Models;

namespace vaultApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Source> Sources { get; set; }
    public DbSet<LibraryItem> LibraryItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<Source>()
            .HasIndex(s => new { s.UserId, s.Url })
            .IsUnique();

        modelBuilder.Entity<LibraryItem>()
            .HasIndex(l => new { l.UserId, l.SourceId, l.GameIndex })
            .IsUnique();
    }
}
