using SessionManagement.Application.Features.SessionSnapshots;

namespace SessionManagement.Application.Features.EvidenceSubmissions;

public sealed record SubmitEvidenceResponse(
    Guid LiveSessionId,
    Guid SessionTeamId,
    Guid EvidenceSubmissionId,
    string ValidationOutcome,
    string ProgressState,
    CurrentSessionStageSnapshot? CurrentStage,
    long SequenceNumber,
    DateTimeOffset SubmittedAtUtc);

public interface ICurrentOperatorIdentity
{
    string GetRequiredOperatorUserId();
}

