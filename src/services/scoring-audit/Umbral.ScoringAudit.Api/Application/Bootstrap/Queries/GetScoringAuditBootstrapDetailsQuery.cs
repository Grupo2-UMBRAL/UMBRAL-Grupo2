using MediatR;
using Umbral.ServiceDefaults;

namespace Umbral.ScoringAudit.Api.Application.Bootstrap.Queries;

public sealed record GetScoringAuditBootstrapDetailsQuery : IRequest<ServiceBootstrapDetails>;

public sealed class GetScoringAuditBootstrapDetailsQueryHandler(IServiceBootstrapDetailsProvider bootstrapDetailsProvider)
    : IRequestHandler<GetScoringAuditBootstrapDetailsQuery, ServiceBootstrapDetails>
{
    public Task<ServiceBootstrapDetails> Handle(
        GetScoringAuditBootstrapDetailsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(bootstrapDetailsProvider.GetBootstrapDetails());
    }
}
