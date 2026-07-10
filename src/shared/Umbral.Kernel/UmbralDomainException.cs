namespace Umbral.ServiceDefaults;

public sealed class UmbralDomainException(
    string code,
    string message,
    UmbralFailureCategory category = UmbralFailureCategory.Domain)
    : UmbralServiceException(code, message, category)
{
}
