using MediatR;
using SessionManagement.Application.Features.SessionSnapshots;

namespace SessionManagement.Application.Features.Hints;

public sealed record ReleaseHintCommand(Guid LiveSessionId, Guid? SessionTeamId, Guid HintId)
    : IRequest<IReadOnlyList<VisibleHintSnapshot>>;

/// <summary>
/// Chooses who receives a Hint when an operator releases it.
/// </summary>
/// <param name="SessionTeamId" example="6f2e5d84-1a7b-4c39-9e02-5b8d3f7a1c46">Single team to release the Hint to. null (or an empty uuid) = broadcast: the Hint goes to every Session Team currently standing on the Hint's Play, skipping teams that already have it. The broadcast is rejected when no team is eligible.</param>
public sealed record ReleaseHintRequest(Guid? SessionTeamId);

