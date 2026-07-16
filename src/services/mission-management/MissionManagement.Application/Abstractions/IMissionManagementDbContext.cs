using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MissionManagement.Domain.Missions;

namespace MissionManagement.Application.Abstractions;

public interface IMissionManagementDbContext
{
    DbSet<Mission> Missions { get; }

    DbSet<PathItem> PathItems { get; }

    DbSet<Section> Sections { get; }

    DbSet<Challenge> Challenges { get; }

    DbSet<Play> Plays { get; }

    DbSet<Choice> Choices { get; }

    DbSet<Hint> Hints { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Opens a transaction so a multi-flush write stays atomic.</summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}
