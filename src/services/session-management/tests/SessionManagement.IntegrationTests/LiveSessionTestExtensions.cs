using SessionManagement.Domain.LiveSessions.States;
using System;
using SessionManagement.Domain.LiveSessions;

namespace SessionManagement.IntegrationTests;

internal static class LiveSessionTestExtensions
{
    public static void ForceState(this LiveSession liveSession, string state)
    {
        var targetState = LiveSessionStateFactory.FromName(state);
        var now = DateTimeOffset.UtcNow;
        if (liveSession.State.Name == targetState.Name) return;

        if (liveSession.State is ScheduledState)
        {
            liveSession.Start(now);
        }

        if (targetState is ActiveState)
        {
            liveSession.ClearDomainEvents();
            return;
        }

        if (targetState is PausedState)
        {
            liveSession.Pause();
            liveSession.ClearDomainEvents();
            return;
        }
        
        if (targetState is CanceledState)
        {
            liveSession.Cancel();
            liveSession.ClearDomainEvents();
            return;
        }

        if (targetState is FinalizedState)
        {
            if (liveSession.State is not CanceledState)
            {
                liveSession.FinalizeSession();
            }
            liveSession.ClearDomainEvents();
            return;
        }

        throw new NotSupportedException($"Cannot force transition to state {state}");
    }
}
