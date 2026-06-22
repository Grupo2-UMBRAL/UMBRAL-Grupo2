using MediatR;

namespace SessionManagement.Application.Features.SessionSnapshots;

public sealed record GetSessionTeamSnapshotQuery(Guid SessionTeamId) : IRequest<SessionTeamSnapshot>;

