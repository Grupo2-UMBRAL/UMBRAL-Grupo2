using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MissionManagement.Domain.Missions;

namespace MissionManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// TPH base for the Composite. <c>item_kind</c> discriminates Section vs Challenge. The Mission FK
/// cascades; the self-referencing Section -> child link is restricted (configured on
/// <see cref="SectionConfiguration"/>) to avoid multiple cascade paths on Postgres.
/// </summary>
internal sealed class PathItemConfiguration : IEntityTypeConfiguration<PathItem>
{
    public const string SectionDiscriminator = "Section";
    public const string ChallengeDiscriminator = "Challenge";

    public void Configure(EntityTypeBuilder<PathItem> builder)
    {
        builder.ToTable("path_items");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(item => item.MissionId)
            .HasColumnName("mission_id")
            .IsRequired();
        builder.Property(item => item.ParentSectionId)
            .HasColumnName("parent_section_id");
        builder.Property(item => item.Order)
            .HasColumnName("order")
            .IsRequired();

        builder.HasDiscriminator<string>("item_kind")
            .HasValue<Section>(SectionDiscriminator)
            .HasValue<Challenge>(ChallengeDiscriminator);

        builder.Property("item_kind")
            .HasColumnName("item_kind")
            .HasMaxLength(20);

        builder.HasIndex(item => new { item.MissionId, item.ParentSectionId, item.Order })
            .IsUnique();

        // Mission FK (no navigation on Mission). Cascade so deleting a mission clears its path items.
        builder.HasOne<Mission>()
            .WithMany()
            .HasForeignKey(item => item.MissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
