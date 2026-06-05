namespace Umbral.ServiceDefaults;

public abstract class UmbralServiceException : Exception
{
    protected UmbralServiceException(
        string code,
        string message,
        UmbralFailureCategory category,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        Code = code;
        Category = category;
    }

    public string Code { get; }

    public UmbralFailureCategory Category { get; }
}
