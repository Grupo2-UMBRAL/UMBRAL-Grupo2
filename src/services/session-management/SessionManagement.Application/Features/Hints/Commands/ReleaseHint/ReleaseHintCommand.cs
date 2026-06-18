using MediatR;
using SessionManagement.Application.Features.SessionSnapshots;

namespace SessionManagement.Application.Features.Hints;

public sealed record ReleaseHintCommand(Guid LiveSessionId, Guid? SessionTeamId, Guid HintId)
    : IRequest<IReadOnlyList<VisibleHintSnapshot>>;

public sealed record ReleaseHintRequest(Guid? SessionTeamId);
