namespace SessionManagement.Domain.LiveSessions;

public static class LiveSessionStates
{
    public const string Scheduled = "Scheduled";
    public const string Active = "Active";
    public const string Running = Active;
    public const string Paused = "Paused";
    public const string Finalized = "Finalized";
    public const string Canceled = "Canceled";
    public const string Cancelled = Canceled;
}
