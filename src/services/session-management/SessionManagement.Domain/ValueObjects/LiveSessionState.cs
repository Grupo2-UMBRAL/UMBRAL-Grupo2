using System;
using System.Collections.Generic;
using System.Linq;

namespace SessionManagement.Domain.LiveSessions;

public sealed record LiveSessionState
{
    public static readonly LiveSessionState Scheduled = new(nameof(Scheduled));
    public static readonly LiveSessionState Active = new(nameof(Active));
    public static readonly LiveSessionState Paused = new(nameof(Paused));
    public static readonly LiveSessionState Finalized = new(nameof(Finalized));
    public static readonly LiveSessionState Canceled = new(nameof(Canceled));

    public string Value { get; }

    private LiveSessionState(string value)
    {
        Value = value;
    }

    public static LiveSessionState FromName(string name)
    {
        var state = All.FirstOrDefault(s => s.Value.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (state is null)
        {
            // Support legacy aliases
            if (name.Equals("Running", StringComparison.OrdinalIgnoreCase)) return Active;
            if (name.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)) return Canceled;

            throw new InvalidOperationException($"Invalid LiveSession state: {name}");
        }

        return state;
    }

    public static IReadOnlyCollection<LiveSessionState> All => new[]
    {
        Scheduled,
        Active,
        Paused,
        Finalized,
        Canceled
    };

    public override string ToString() => Value;
}
