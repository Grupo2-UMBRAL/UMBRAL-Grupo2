using System;
using SessionManagement.Domain.Abstractions;
using Umbral.ServiceDefaults;

namespace SessionManagement.Domain.LiveSessions.States;

public sealed class ScheduledState : ILiveSessionState
{
    public string Name => "Scheduled";

    public void Start(LiveSession session, DateTimeOffset startedAtUtc)
    {
        if (session.SessionTeams.Count == 0)
        {
            throw new UmbralDomainException(
                "live_session_requires_session_teams",
                "LiveSession cannot start without at least one Session Team registered.",
                UmbralFailureCategory.Conflict);
        }

        if (session.EnrollmentWindowOpenedAtUtc is not null && session.EnrollmentWindowClosedAtUtc is null)
        {
            session.CloseEnrollmentWindow(startedAtUtc);
        }

        session.ChangeState(new ActiveState());
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
        throw new UmbralDomainException(
            "live_session_cannot_finalize",
            "Only an Active or Paused LiveSession can finalize.",
            UmbralFailureCategory.Conflict);
    }

    public void Cancel(LiveSession session)
    {
        session.ChangeState(new CanceledState());
    }

    public void EnsureCanModifyEnrollment()
    {
        // Allowed
    }

    public void EnsureCanSubmitEvidence()
    {
        // Allowed
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
        // Allowed
    }

    public void EnsureCanPenalize()
    {
        throw new UmbralDomainException(
            "live_session_cannot_penalize",
            "LiveSession must be Active or Paused to apply a Penalty.",
            UmbralFailureCategory.Conflict);
    }
}
