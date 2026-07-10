namespace SessionManagement.Infrastructure.Messaging;

/// <summary>
/// Topology names for the audit-event pipeline. Intentionally duplicated in
/// scoring-monitoring: a queue contract is like an HTTP contract — each bounded
/// context owns its own copy of the shape rather than sharing it via src/shared
/// (ADR-012 keeps business contracts out of the shared kernel).
/// </summary>
internal static class UmbralAuditMessaging
{
    internal const string Exchange = "umbral.events";
    internal const string RoutingKey = "session.audit.logged";
    internal const string Queue = "scoring.audit.session-events";
}
