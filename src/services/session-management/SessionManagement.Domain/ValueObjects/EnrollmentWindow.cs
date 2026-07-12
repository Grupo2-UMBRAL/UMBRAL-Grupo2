namespace SessionManagement.Domain.LiveSessions;

public sealed record EnrollmentWindow(DateTimeOffset? OpenedAtUtc, DateTimeOffset? ClosedAtUtc)
{
    public bool IsOpenAt(DateTimeOffset nowUtc)
    {
        if (OpenedAtUtc is null)
        {
            return false;
        }

        if (ClosedAtUtc is not null)
        {
            return false;
        }

        return OpenedAtUtc <= nowUtc;
    }
}
