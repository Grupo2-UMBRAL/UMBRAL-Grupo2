using System;
using SessionManagement.Domain.Abstractions;
using Umbral.ServiceDefaults;

namespace SessionManagement.Domain.LiveSessions.States;

public sealed class ActiveState : ILiveSessionState
{
    public string Name => "Active";

    public void Start(LiveSession session, DateTimeOffset startedAtUtc)
    {
        throw new UmbralDomainException(
            "live_session_cannot_start",
            "Only a Scheduled LiveSession can start.",
            UmbralFailureCategory.Conflict);
    }

    public void Pause(LiveSession session)
    {
        session.ChangeState(new PausedState());
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
        // Allowed
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
