using MediatR;
using Umbral.ServiceDefaults;

namespace Umbral.ScoringAudit.Api.Application.Bootstrap.Commands;

public sealed record ApplyScoringAuditPersistenceMigrationsCommand : IRequest;

public sealed class ApplyScoringAuditPersistenceMigrationsCommandHandler(IServicePersistenceInitializer persistenceInitializer)
    : IRequestHandler<ApplyScoringAuditPersistenceMigrationsCommand>
{
    public async Task<Unit> Handle(
        ApplyScoringAuditPersistenceMigrationsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await persistenceInitializer.ApplyPendingMigrationsAsync(cancellationToken);

        return Unit.Value;
    }
}
