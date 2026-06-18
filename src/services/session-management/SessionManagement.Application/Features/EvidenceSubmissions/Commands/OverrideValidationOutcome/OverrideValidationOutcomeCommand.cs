using MediatR;
using SessionManagement.Application.Features.SessionSnapshots;

namespace SessionManagement.Application.Features.EvidenceSubmissions;

public sealed record OverrideValidationOutcomeCommand(
    Guid EvidenceSubmissionId,
    bool IsAccepted,
    string Reason) : IRequest<OverrideValidationOutcomeResponse>;

public sealed record OverrideValidationOutcomeRequest(bool IsAccepted, string Reason);

public sealed record OverrideValidationOutcomeResponse(
    Guid LiveSessionId,
    Guid EvidenceSubmissionId,
    Guid ValidationOverrideLogId,
    Guid SessionTeamId,
    Guid MissionStageId,
    string PreviousOutcome,
    string NewOutcome,
    string ProgressState,
    CurrentSessionStageSnapshot? CurrentStage,
    long SequenceNumber,
    DateTimeOffset OverriddenAtUtc);
