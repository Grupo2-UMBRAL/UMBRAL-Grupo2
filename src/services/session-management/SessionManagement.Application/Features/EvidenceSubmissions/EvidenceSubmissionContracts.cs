using SessionManagement.Application.Features.SessionSnapshots;

namespace SessionManagement.Application.Features.EvidenceSubmissions;

/// <summary>
/// Outcome of an Evidence Submission, for both Trivia answers and Treasure Hunt scans. The
/// submission is recorded as an operational fact whether or not it was accepted; scoring is decided
/// separately by Scoring and Monitoring.
/// </summary>
/// <param name="LiveSessionId" example="5e1d9f34-8b26-4a7c-9f10-3c2b5d6e7a80">Session the attempt belongs to.</param>
/// <param name="SessionTeamId" example="6f2e5d84-1a7b-4c39-9e02-5b8d3f7a1c46">Team that made the attempt.</param>
/// <param name="EvidenceSubmissionId" example="8c1f3b57-4e29-4d6a-b083-2f9e7c5d1a48">The recorded attempt. An operator needs this id to apply a Validation Override later.</param>
/// <param name="ValidationOutcome" example="Accepted">Accepted or Rejected, decided server-side. Rejected is an Invalid Attempt: it is audited but subtracts nothing on its own.</param>
/// <param name="ProgressState" example="InProgress">This team's state after the attempt: NotStarted, InProgress or Completed. Turns Completed when an accepted attempt closes the last Play.</param>
/// <param name="CurrentStage">Play to render after the attempt: the next one when accepted, the same one when rejected. null once the team has finished the Session Flow.</param>
/// <param name="SequenceNumber" example="43">LiveSession counter after this attempt. Lets clients order this response against realtime events.</param>
/// <param name="SubmittedAtUtc" example="2026-07-16T21:14:05Z">Backend reception instant, the official Resolution Time reference used for ranking tie-breaks and auditing.</param>
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

