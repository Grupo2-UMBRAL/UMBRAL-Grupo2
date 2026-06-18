using Microsoft.EntityFrameworkCore;
using SessionOperations.Domain.LiveSessions;

namespace SessionOperations.Application.Abstractions;

public interface ISessionOperationsDbContext
{
    DbSet<LiveSession> LiveSessions { get; }
    DbSet<SessionTeam> SessionTeams { get; }
    DbSet<TeamParticipation> TeamParticipations { get; }
    DbSet<SessionTeamProgress> TeamProgressions { get; }
    DbSet<EvidenceSubmission> EvidenceSubmissions { get; }
    DbSet<ValidationOverrideLog> ValidationOverrideLogs { get; }
    DbSet<ReleasedHint> ReleasedHints { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
