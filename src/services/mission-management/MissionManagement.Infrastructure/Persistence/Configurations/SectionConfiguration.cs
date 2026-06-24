using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MissionManagement.Domain.Missions;

namespace MissionManagement.Infrastructure.Persistence.Configurations;

internal sealed class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    public void Configure(EntityTypeBuilder<Section> builder)
    {
        builder.Property(section => section.Title)
            .HasColumnName("title")
            .HasMaxLength(120);

        // Self-referencing composite: a Section owns child path items via ParentSectionId.
        // Restrict (not cascade) avoids multiple cascade paths on Postgres; the app deletes children
        // explicitly (MissionLoader.DeleteItemsAsync removes all path items for a mission).
        builder.HasMany(section => section.Children)
            .WithOne()
            .HasForeignKey(item => item.ParentSectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(section => section.Children)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
