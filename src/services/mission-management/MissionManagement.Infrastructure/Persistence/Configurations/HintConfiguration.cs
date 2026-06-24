using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MissionManagement.Domain.Missions;

namespace MissionManagement.Infrastructure.Persistence.Configurations;

internal sealed class HintConfiguration : IEntityTypeConfiguration<Hint>
{
    public void Configure(EntityTypeBuilder<Hint> builder)
    {
        builder.ToTable("hints");
        builder.HasKey(hint => hint.Id);

        builder.Property(hint => hint.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(hint => hint.SearchId)
            .HasColumnName("search_id")
            .IsRequired();
        builder.Property(hint => hint.Order)
            .HasColumnName("order")
            .IsRequired();
        builder.Property(hint => hint.Content)
            .HasColumnName("content")
            .HasMaxLength(1_024)
            .IsRequired();
        builder.Property(hint => hint.IsSolution)
            .HasColumnName("is_solution")
            .IsRequired();
        builder.Property(hint => hint.Latitude)
            .HasColumnName("latitude");
        builder.Property(hint => hint.Longitude)
            .HasColumnName("longitude");

        builder.HasIndex(hint => new { hint.SearchId, hint.Order })
            .IsUnique();
    }
}
