using Umbral.ServiceDefaults;

namespace MissionManagement.Domain.Missions;

internal static class DomainText
{
    public static string NormalizeRequired(
        string? value,
        string errorCode,
        string errorMessage,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UmbralDomainException(errorCode, errorMessage, UmbralFailureCategory.Validation);
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new UmbralDomainException(
                $"{errorCode}_too_long",
                $"Value cannot exceed {maximumLength} characters.",
                UmbralFailureCategory.Validation);
        }

        return normalized;
    }

    public static string? NormalizeOptional(string? value, string errorCode, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new UmbralDomainException(
                $"{errorCode}_too_long",
                $"Value cannot exceed {maximumLength} characters.",
                UmbralFailureCategory.Validation);
        }

        return normalized;
    }

    public static int NormalizeOrder(int order, string errorCode, string errorMessage)
    {
        if (order <= 0)
        {
            throw new UmbralDomainException(errorCode, errorMessage, UmbralFailureCategory.Validation);
        }

        return order;
    }
}
