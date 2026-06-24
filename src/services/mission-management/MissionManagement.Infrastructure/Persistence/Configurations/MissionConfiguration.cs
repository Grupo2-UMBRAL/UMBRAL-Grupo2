using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MissionManagement.Domain.Missions;

namespace MissionManagement.Infrastructure.Persistence.Configurations;

internal sealed class MissionConfiguration : IEntityTypeConfiguration<Mission>
{
    public void Configure(EntityTypeBuilder<Mission> builder)
    {
        builder.ToTable("missions");
        builder.HasKey(mission => mission.Id);

        builder.Property(mission => mission.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(mission => mission.Name)
            .HasColumnName("name")
            .HasMaxLength(120)
            .IsRequired();
        builder.Property(mission => mission.Description)
            .HasColumnName("description")
            .HasMaxLength(1_024)
            .IsRequired();
        builder.Property(mission => mission.MaximumDurationMinutes)
            .HasColumnName("maximum_duration_minutes")
            .IsRequired();
        builder.Property(mission => mission.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        // The aggregate has no EF navigation to its path items; they are loaded/saved explicitly.
        builder.Ignore(mission => mission.RootItems);

        builder.HasIndex(mission => mission.Name)
            .IsUnique();
    }
}
