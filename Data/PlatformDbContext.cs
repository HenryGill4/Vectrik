using Microsoft.EntityFrameworkCore;
using Vectrik.Models.Platform;

namespace Vectrik.Data;

public class PlatformDbContext : DbContext
{
    public PlatformDbContext(DbContextOptions<PlatformDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<PlatformUser> PlatformUsers { get; set; }
    public DbSet<TenantFeatureFlag> TenantFeatureFlags { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
        });

        modelBuilder.Entity<PlatformUser>(entity =>
        {
            entity.HasIndex(e => e.Username).IsUnique();
        });

        modelBuilder.Entity<TenantFeatureFlag>(entity =>
        {
            entity.HasIndex(e => new { e.TenantCode, e.FeatureKey }).IsUnique();
        });
    }
}
