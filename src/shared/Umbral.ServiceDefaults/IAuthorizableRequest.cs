namespace Umbral.ServiceDefaults;

public interface IAuthorizableRequest
{
    RequestAuthorizationMetadata Authorization { get; }
}
