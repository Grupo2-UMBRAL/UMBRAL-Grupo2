using Microsoft.EntityFrameworkCore;

namespace Umbral.SessionOperations.Api.Infrastructure;

public sealed class SessionOperationsDbContext(DbContextOptions<SessionOperationsDbContext> options) : DbContext(options);
