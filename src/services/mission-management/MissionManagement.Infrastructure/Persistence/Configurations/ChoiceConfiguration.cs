using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MissionManagement.Domain.Missions;

namespace MissionManagement.Infrastructure.Persistence.Configurations;

internal sealed class ChoiceConfiguration : IEntityTypeConfiguration<Choice>
{
    public void Configure(EntityTypeBuilder<Choice> builder)
    {
        builder.ToTable("choices");
        builder.HasKey(choice => choice.Id);

        builder.Property(choice => choice.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(choice => choice.QuestionId)
            .HasColumnName("question_id")
            .IsRequired();
        builder.Property(choice => choice.Order)
            .HasColumnName("order")
            .IsRequired();
        builder.Property(choice => choice.Text)
            .HasColumnName("text")
            .HasMaxLength(512)
            .IsRequired();
        builder.Property(choice => choice.IsCorrect)
            .HasColumnName("is_correct")
            .IsRequired();

        builder.HasIndex(choice => new { choice.QuestionId, choice.Order })
            .IsUnique();
    }
}
