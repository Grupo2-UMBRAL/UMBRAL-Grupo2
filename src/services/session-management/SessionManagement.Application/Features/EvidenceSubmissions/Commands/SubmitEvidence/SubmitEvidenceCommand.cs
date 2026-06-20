using MediatR;
using SessionManagement.Application.Features.SessionSnapshots;

namespace SessionManagement.Application.Features.EvidenceSubmissions;

public sealed record SubmitEvidenceCommand(Guid SessionTeamId, string QrHash) : IRequest<SubmitEvidenceResponse>;

public sealed record SubmitEvidenceRequest(string QrHash);

