using Microsoft.EntityFrameworkCore;
using SessionManagement.Domain.LiveSessions;

namespace SessionManagement.Application.Abstractions;

public interface ISessionManagementDbContext
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
