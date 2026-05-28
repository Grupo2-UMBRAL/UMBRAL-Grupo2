using MediatR;
using Umbral.ServiceDefaults;

namespace Umbral.SessionOperations.Api.Application.Bootstrap.Queries;

public sealed record GetSessionOperationsBootstrapDetailsQuery : IRequest<ServiceBootstrapDetails>;

public sealed class GetSessionOperationsBootstrapDetailsQueryHandler(IServiceBootstrapDetailsProvider bootstrapDetailsProvider)
    : IRequestHandler<GetSessionOperationsBootstrapDetailsQuery, ServiceBootstrapDetails>
{
    public Task<ServiceBootstrapDetails> Handle(
        GetSessionOperationsBootstrapDetailsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(bootstrapDetailsProvider.GetBootstrapDetails());
    }
}
