using Microsoft.EntityFrameworkCore;

namespace Umbral.MissionDesign.Api.Infrastructure;

public sealed class MissionDesignDbContext(DbContextOptions<MissionDesignDbContext> options) : DbContext(options);
