using MediatR;
using SessionManagement.Application.Features.SessionSnapshots;

namespace SessionManagement.Application.Features.EvidenceSubmissions;

public sealed record OverrideValidationOutcomeCommand(
    Guid EvidenceSubmissionId,
    bool IsAccepted,
    string Reason) : IRequest<OverrideValidationOutcomeResponse>;

/// <summary>
/// Operator's manual correction of an ambiguous Evidence Submission. Exceptional by design: normal
/// Trivia validation is automatic and server-side.
/// </summary>
/// <param name="IsAccepted">true = treat the attempt as valid, which enables the Play's full credit once and advances the team. Credit is never granted twice for the same Play.</param>
/// <param name="Reason" example="QR unreadable at the site; verified in person">Justification stored in the audit trail. Required, since no override may change a result silently.</param>
public sealed record OverrideValidationOutcomeRequest(bool IsAccepted, string Reason);

/// <summary>
/// Result of a Validation Override, including the audit record it produced and the team's state
/// after the correction.
/// </summary>
/// <param name="LiveSessionId" example="5e1d9f34-8b26-4a7c-9f10-3c2b5d6e7a80">Session the corrected attempt belongs to.</param>
/// <param name="EvidenceSubmissionId" example="8c1f3b57-4e29-4d6a-b083-2f9e7c5d1a48">Attempt that was corrected.</param>
/// <param name="ValidationOverrideLogId" example="1e5b8d20-3f7c-4a96-8b41-9d2e0c6f5a37">Audit entry recording who overrode what and why.</param>
/// <param name="SessionTeamId" example="6f2e5d84-1a7b-4c39-9e02-5b8d3f7a1c46">Team affected by the correction.</param>
/// <param name="MissionStageId" example="2b8c47f1-9a3e-4d56-b7c8-1e9f0a2d3b64">Play the corrected attempt was made on.</param>
/// <param name="PreviousOutcome" example="Rejected">Accepted or Rejected before the override.</param>
/// <param name="NewOutcome" example="Accepted">Accepted or Rejected after the override; this is the final operational result.</param>
/// <param name="ProgressState" example="InProgress">Team state after the correction: NotStarted, InProgress or Completed.</param>
/// <param name="CurrentStage">Play the team stands on after the correction; accepting an attempt moves it forward. null once the team has finished the Session Flow.</param>
/// <param name="SequenceNumber" example="44">LiveSession counter after the override, for ordering against realtime events.</param>
/// <param name="OverriddenAtUtc" example="2026-07-16T21:20:00Z">Instant the correction was recorded. It does not replace the attempt's original Resolution Time.</param>
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

