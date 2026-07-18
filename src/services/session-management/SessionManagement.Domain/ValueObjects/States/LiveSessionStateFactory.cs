using System;
using System.Collections.Generic;
using System.Linq;

namespace SessionManagement.Domain.LiveSessions.States;

public static class LiveSessionStateFactory
{
    private static readonly IReadOnlyCollection<ILiveSessionState> AllStates = new ILiveSessionState[]
    {
        new ScheduledState(),
        new ActiveState(),
        new PausedState(),
        new FinalizedState(),
        new CanceledState()
    };

    public static ILiveSessionState FromName(string name)
    {
        var state = AllStates.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (state is null)
        {
            // Support legacy aliases mapped previously
            if (name.Equals("Running", StringComparison.OrdinalIgnoreCase)) return new ActiveState();
            if (name.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)) return new CanceledState();

            throw new InvalidOperationException($"Invalid LiveSession state: {name}");
        }

        return state;
    }
}
