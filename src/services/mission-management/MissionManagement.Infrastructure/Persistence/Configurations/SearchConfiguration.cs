using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MissionManagement.Domain.Missions;

namespace MissionManagement.Infrastructure.Persistence.Configurations;

internal sealed class SearchConfiguration : IEntityTypeConfiguration<Search>
{
    public void Configure(EntityTypeBuilder<Search> builder)
    {
        builder.Property(search => search.Clue)
            .HasColumnName("clue")
            .HasMaxLength(1_024);
        builder.Property(search => search.ExpectedQrHash)
            .HasColumnName("expected_qr_hash")
            .HasMaxLength(256);

        // A Search owns its hints. Cascade on delete.
        builder.HasMany(search => search.Hints)
            .WithOne()
            .HasForeignKey(hint => hint.SearchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(search => search.Hints)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
