using System;
using SessionManagement.Domain.LiveSessions;

namespace SessionManagement.IntegrationTests;

internal static class LiveSessionTestExtensions
{
    public static void ForceState(this LiveSession liveSession, string state)
    {
        var targetState = LiveSessionState.FromName(state);
        var now = DateTimeOffset.UtcNow;
        if (liveSession.State == targetState) return;

        if (liveSession.State == LiveSessionState.Scheduled)
        {
            liveSession.Start(now);
        }

        if (targetState == LiveSessionState.Active)
        {
            liveSession.ClearDomainEvents();
            return;
        }

        if (targetState == LiveSessionState.Paused)
        {
            liveSession.Pause();
            liveSession.ClearDomainEvents();
            return;
        }
        
        if (targetState == LiveSessionState.Canceled)
        {
            liveSession.Cancel();
            liveSession.ClearDomainEvents();
            return;
        }

        if (targetState == LiveSessionState.Finalized)
        {
            if (liveSession.State != LiveSessionState.Canceled)
            {
                liveSession.FinalizeSession();
            }
            liveSession.ClearDomainEvents();
            return;
        }

        throw new NotSupportedException($"Cannot force transition to state {state}");
    }
}
