using System;
using SessionManagement.Domain.Abstractions;
using Umbral.ServiceDefaults;

namespace SessionManagement.Domain.LiveSessions.States;

public sealed class FinalizedState : ILiveSessionState
{
    public string Name => "Finalized";

    public void Start(LiveSession session, DateTimeOffset startedAtUtc)
    {
        throw new UmbralDomainException(
            "live_session_cannot_start",
            "Only a Scheduled LiveSession can start.",
            UmbralFailureCategory.Conflict);
    }

    public void Pause(LiveSession session)
    {
        throw new UmbralDomainException(
            "live_session_cannot_pause",
            "Only an Active LiveSession can pause.",
            UmbralFailureCategory.Conflict);
    }

    public void Resume(LiveSession session)
    {
        throw new UmbralDomainException(
            "live_session_cannot_resume",
            "Only a Paused LiveSession can resume.",
            UmbralFailureCategory.Conflict);
    }

    public void FinalizeSession(LiveSession session)
    {
        // No-op, already finalized
    }

    public void Cancel(LiveSession session)
    {
        throw new UmbralDomainException(
            "live_session_cannot_cancel",
            "Only a Scheduled, Active or Paused LiveSession can cancel.",
            UmbralFailureCategory.Conflict);
    }

    public void EnsureCanModifyEnrollment()
    {
        throw new UmbralDomainException(
            "live_session_not_scheduled",
            "LiveSession must be scheduled to accept enrollment changes.",
            UmbralFailureCategory.Conflict);
    }

    public void EnsureCanSubmitEvidence()
    {
        throw new UmbralDomainException(
            "live_session_not_accepting_evidence",
            "LiveSession is not accepting Evidence Submissions.",
            UmbralFailureCategory.Conflict);
    }

    public void EnsureCanReleaseHint()
    {
        throw new UmbralDomainException(
            "live_session_not_accepting_hint_release",
            "LiveSession must be Active or Paused to release or create Hints.",
            UmbralFailureCategory.Conflict);
    }

    public void EnsureCanDeactivateStage()
    {
        throw new UmbralDomainException(
            "live_session_stage_deactivation_not_allowed_for_state",
            "LiveSession must be Scheduled, Active or Paused to deactivate a pending Session Stage.",
            UmbralFailureCategory.Conflict);
    }

    public void EnsureCanPenalize()
    {
        throw new UmbralDomainException(
            "live_session_cannot_penalize",
            "LiveSession must be Active or Paused to apply a Penalty.",
            UmbralFailureCategory.Conflict);
    }
}
