using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShoMark.Domain.Entities;

namespace ShoMark.Infrastructure.Data.Configurations;

public class PlatformConfiguration : IEntityTypeConfiguration<Platform>
{
    public void Configure(EntityTypeBuilder<Platform> builder)
    {
        builder.ToTable("platforms");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(p => p.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(p => p.PlatformType).HasColumnName("platform_type").HasMaxLength(20)
            .HasConversion<string>();
        builder.Property(p => p.AccountName).HasColumnName("account_name").HasMaxLength(255);
        builder.Property(p => p.AccessToken).HasColumnName("access_token").HasMaxLength(2000);
        builder.Property(p => p.RefreshToken).HasColumnName("refresh_token").HasMaxLength(2000);
        builder.Property(p => p.TokenExpiresAt).HasColumnName("token_expires_at");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        builder.HasIndex(p => p.UserId);
        builder.HasIndex(p => p.TokenExpiresAt);
    }
}
