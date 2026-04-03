namespace Umbral.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Umbral.Domain.Missions.Entities;
using Umbral.Domain.Scoring.Entities;
using Umbral.Domain.Sessions.Entities;
using Umbral.Domain.Teams.Entities;
using Umbral.Domain.Users.Entities;

public class UmbralDbContext : DbContext
{
    // Bounded Context: Users
    public DbSet<User> Users => Set<User>();

    // Bounded Context: Missions
    public DbSet<Mission> Missions => Set<Mission>();
    public DbSet<MissionStage> MissionStages => Set<MissionStage>();
    public DbSet<Clue> Clues => Set<Clue>();

    // Bounded Context: Sessions
    public DbSet<LiveSession> Sessions => Set<LiveSession>();
    public DbSet<SessionEvent> SessionEvents => Set<SessionEvent>();

    // Bounded Context: Teams
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Evidence> Evidences => Set<Evidence>();
    public DbSet<TeamClueRelease> TeamClueReleases => Set<TeamClueRelease>();
    public DbSet<TeamStageProgress> TeamStageProgresses => Set<TeamStageProgress>();

    // Bounded Context: Scoring
    public DbSet<ScoreEntry> ScoreEntries => Set<ScoreEntry>();
    public DbSet<Penalty> Penalties => Set<Penalty>();

    // Outbox
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public UmbralDbContext(DbContextOptions<UmbralDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UmbralDbContext).Assembly);
    }
}
