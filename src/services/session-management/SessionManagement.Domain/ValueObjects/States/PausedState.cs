using System;
using SessionManagement.Domain.Abstractions;
using Umbral.ServiceDefaults;

namespace SessionManagement.Domain.LiveSessions.States;

public sealed class PausedState : ILiveSessionState
{
    public string Name => "Paused";

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
        session.ChangeState(new ActiveState());
    }

    public void FinalizeSession(LiveSession session)
    {
        session.ChangeState(new FinalizedState());
    }

    public void Cancel(LiveSession session)
    {
        session.ChangeState(new CanceledState());
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
        // Allowed
    }

    public void EnsureCanDeactivateStage()
    {
        // Allowed
    }

    public void EnsureCanPenalize()
    {
        // Allowed
    }
}
