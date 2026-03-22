using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShoMark.Domain.Entities;

namespace ShoMark.Infrastructure.Data.Configurations;

public class VideoConfiguration : IEntityTypeConfiguration<Video>
{
    public void Configure(EntityTypeBuilder<Video> builder)
    {
        builder.ToTable("videos");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(v => v.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
        builder.Property(v => v.MinioKey).HasColumnName("minio_key").HasMaxLength(500).IsRequired();
        builder.Property(v => v.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(500);
        builder.Property(v => v.DurationSeconds).HasColumnName("duration_seconds");
        builder.Property(v => v.FileSize).HasColumnName("file_size");
        builder.Property(v => v.DeletedAt).HasColumnName("deleted_at");
        builder.Property(v => v.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(v => v.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        builder.HasIndex(v => v.MinioKey).IsUnique();
    }
}
