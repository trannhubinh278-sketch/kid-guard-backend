using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace NhuBinh_2921.Models;

public class ApplicationDbContext : IdentityDbContext<User>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ChildDevice> ChildDevices => Set<ChildDevice>();
    public DbSet<SafetyAlert> SafetyAlerts => Set<SafetyAlert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Product>().Property(p => p.Price).HasPrecision(18, 2);
        modelBuilder.Entity<User>().Property(u => u.Initials).HasMaxLength(5);
        modelBuilder.Entity<Product>()
            .HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Category>().HasIndex(c => c.Name).IsUnique();
        modelBuilder.Entity<ChildDevice>().HasIndex(d => d.PairingCode).IsUnique();
        modelBuilder.Entity<ChildDevice>().HasIndex(d => d.ParentUserId);
        modelBuilder.Entity<ChildDevice>().HasIndex(d => d.ChildUserId);
        modelBuilder.Entity<SafetyAlert>().HasIndex(a => a.ChildDeviceId);
        modelBuilder.HasDefaultSchema("identity");
    }
}
