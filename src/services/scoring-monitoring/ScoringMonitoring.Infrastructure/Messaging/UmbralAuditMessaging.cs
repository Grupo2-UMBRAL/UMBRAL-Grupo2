namespace ScoringMonitoring.Infrastructure.Messaging;

/// <summary>
/// Topology names for the audit-event pipeline. Structurally equal to the copy in
/// session-management: both contexts own their own view of the queue contract rather
/// than sharing it via src/shared (ADR-012 keeps business contracts out of the kernel).
/// </summary>
internal static class UmbralAuditMessaging
{
    internal const string Exchange = "umbral.events";
    internal const string RoutingKey = "session.audit.logged";
    internal const string Queue = "scoring.audit.session-events";
}
