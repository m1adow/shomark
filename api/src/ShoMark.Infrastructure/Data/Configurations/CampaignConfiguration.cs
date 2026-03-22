using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShoMark.Domain.Entities;

namespace ShoMark.Infrastructure.Data.Configurations;

public class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("campaigns");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(c => c.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(c => c.FragmentId).HasColumnName("fragment_id").IsRequired();
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(255);
        builder.Property(c => c.Status).HasColumnName("status").HasMaxLength(20)
            .HasConversion<string>();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        builder.HasOne(c => c.User)
            .WithMany(u => u.Campaigns)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Fragment)
            .WithMany(f => f.Campaigns)
            .HasForeignKey(c => c.FragmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // One campaign per user per fragment
        builder.HasIndex(c => new { c.UserId, c.FragmentId }).IsUnique();
        builder.HasIndex(c => c.UserId);
        builder.HasIndex(c => c.FragmentId);
    }
}
