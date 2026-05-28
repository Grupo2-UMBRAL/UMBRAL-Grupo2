namespace Umbral.ServiceDefaults;

public sealed class UmbralTechnicalException(
    string code,
    string message,
    Exception? innerException = null)
    : UmbralServiceException(code, message, UmbralFailureCategory.Technical, innerException)
{
}
