using Microsoft.EntityFrameworkCore;
using ShoMark.Domain.Entities;

namespace ShoMark.Infrastructure.Data;

public class CampaignDbContext : DbContext
{
    public CampaignDbContext(DbContextOptions<CampaignDbContext> options) : base(options) { }

    public DbSet<Video> Videos => Set<Video>();
    public DbSet<AiFragment> AiFragments => Set<AiFragment>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CampaignDbContext).Assembly);

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
