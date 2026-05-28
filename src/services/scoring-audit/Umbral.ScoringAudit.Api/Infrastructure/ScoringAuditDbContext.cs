using Microsoft.EntityFrameworkCore;

namespace Umbral.ScoringAudit.Api.Infrastructure;

public sealed class ScoringAuditDbContext(DbContextOptions<ScoringAuditDbContext> options) : DbContext(options);
