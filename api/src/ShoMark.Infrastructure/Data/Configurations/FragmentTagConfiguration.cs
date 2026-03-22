using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShoMark.Domain.Entities;

namespace ShoMark.Infrastructure.Data.Configurations;

public class FragmentTagConfiguration : IEntityTypeConfiguration<FragmentTag>
{
    public void Configure(EntityTypeBuilder<FragmentTag> builder)
    {
        builder.ToTable("fragment_tags");

        builder.HasKey(ft => new { ft.FragmentId, ft.TagId });

        builder.Property(ft => ft.FragmentId).HasColumnName("fragment_id");
        builder.Property(ft => ft.TagId).HasColumnName("tag_id");

        builder.HasOne(ft => ft.Fragment)
            .WithMany(f => f.FragmentTags)
            .HasForeignKey(ft => ft.FragmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ft => ft.Tag)
            .WithMany(t => t.FragmentTags)
            .HasForeignKey(ft => ft.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ft => ft.FragmentId);
        builder.HasIndex(ft => ft.TagId);
    }
}
