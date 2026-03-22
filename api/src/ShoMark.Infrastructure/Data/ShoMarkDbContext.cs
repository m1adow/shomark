using Microsoft.EntityFrameworkCore;
using ShoMark.Domain.Entities;

namespace ShoMark.Infrastructure.Data;

public class ShoMarkDbContext : DbContext
{
    public ShoMarkDbContext(DbContextOptions<ShoMarkDbContext> options) : base(options) { }

    public DbSet<Video> Videos => Set<Video>();
    public DbSet<AiFragment> AiFragments => Set<AiFragment>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<FragmentTag> FragmentTags => Set<FragmentTag>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Analytics> Analytics => Set<Analytics>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Platform> Platforms => Set<Platform>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShoMarkDbContext).Assembly);

        // Global query filter for soft-deleted videos
        modelBuilder.Entity<Video>().HasQueryFilter(v => v.DeletedAt == null);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
