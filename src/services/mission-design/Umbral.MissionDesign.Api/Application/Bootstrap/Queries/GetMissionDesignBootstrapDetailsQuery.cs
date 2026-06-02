using MediatR;
using Umbral.ServiceDefaults;

namespace Umbral.MissionDesign.Api.Application.Bootstrap.Queries;

public sealed record GetMissionDesignBootstrapDetailsQuery : IRequest<ServiceBootstrapDetails>, IAuthorizableRequest
{
    public RequestAuthorizationMetadata Authorization => UmbralRequestAuthorizations.AuthenticatedOnly;
}

public sealed class GetMissionDesignBootstrapDetailsQueryHandler(IServiceBootstrapDetailsProvider bootstrapDetailsProvider)
    : IRequestHandler<GetMissionDesignBootstrapDetailsQuery, ServiceBootstrapDetails>
{
    public Task<ServiceBootstrapDetails> Handle(
        GetMissionDesignBootstrapDetailsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(bootstrapDetailsProvider.GetBootstrapDetails());
    }
}
