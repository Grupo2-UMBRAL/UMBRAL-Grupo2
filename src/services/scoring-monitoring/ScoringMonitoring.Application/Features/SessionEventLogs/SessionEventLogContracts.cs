using ScoringMonitoring.Domain.Audit;

namespace ScoringMonitoring.Application.Features.SessionEventLogs;

/// <summary>
/// One entry of the Session Event Log, the auditable history of relevant facts of a LiveSession.
/// It exists for traceability and later supervision, not as a realtime notification or a debug log.
/// </summary>
/// <param name="Id" example="0b3e7c15-9d48-4f62-a1c7-5e8b2d640a93">Identity of the entry. When the originating fact arrives over the at-least-once audit queue it carries its own event id, which becomes this Id — so a redelivery returns the same entry instead of duplicating it.</param>
/// <param name="LiveSessionId" example="c7a1d4e2-8b96-4f31-a0c5-2d7e6f8b1934">The LiveSession the fact belongs to.</param>
/// <param name="EventType" example="PenaltyApplied">Classifies the fact for filtering. Entries written by scoring itself use "StageCredit" and "PenaltyApplied"; other contexts may log their own types.</param>
/// <param name="Description" example="Penalty of severity 'Major' applied to Session Team '3fa85f64-5717-4562-b3fc-2c963f66afa6' by Operator '8c1e4a90-7f3b-4d26-b5a8-0e9c3f2d7614' for reason: external map used. Score variation: -100 points.">Human-readable account of the fact, already rendered for a supervisor to read. Not machine-parsable — treat it as prose.</param>
/// <param name="Timestamp" example="2026-07-16T14:35:20.000Z">When the fact happened. Entries derived from scoring carry the RecordedAt of the originating command, so this is the fact's own instant rather than the moment it was persisted.</param>
public sealed record SessionEventLogPayload(
    Guid Id,
    Guid LiveSessionId,
    string EventType,
    string Description,
    DateTimeOffset Timestamp)
{
    public static SessionEventLogPayload FromEntity(SessionEventLog eventLog)
    {
        ArgumentNullException.ThrowIfNull(eventLog);

        return new SessionEventLogPayload(
            eventLog.Id,
            eventLog.LiveSessionId,
            eventLog.EventType,
            eventLog.Description,
            eventLog.Timestamp);
    }
}

/// <summary>
/// Records an auditable fact against a LiveSession's Session Event Log.
/// The LiveSession comes from the route, and the entry is timestamped by the server on arrival.
/// </summary>
/// <param name="EventType" example="HintReleased">Classifies the fact for filtering. Free-form, so callers should reuse a stable value per kind of fact rather than inventing one per call.</param>
/// <param name="Description" example="Operator released hint 2 for Session Team 'Los Andes' after the stage timer expired.">Human-readable account of the fact, stored verbatim. This is what a supervisor reads later, so it should stand on its own without the caller's context.</param>
public sealed record LogSessionEventRequest(string EventType, string Description);

