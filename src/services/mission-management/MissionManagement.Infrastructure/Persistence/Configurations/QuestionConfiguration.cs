using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MissionManagement.Domain.Missions;

namespace MissionManagement.Infrastructure.Persistence.Configurations;

internal sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.Property(question => question.Text)
            .HasColumnName("text")
            .HasMaxLength(1_024);

        // A Question owns its choices. Cascade on delete.
        builder.HasMany(question => question.Choices)
            .WithOne()
            .HasForeignKey(choice => choice.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(question => question.Choices)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
