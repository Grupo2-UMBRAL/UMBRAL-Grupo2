using MediatR;
using Umbral.ServiceDefaults;

namespace Umbral.SessionOperations.Api.Application.Bootstrap.Commands;

public sealed record ApplySessionOperationsPersistenceMigrationsCommand : IRequest;

public sealed class ApplySessionOperationsPersistenceMigrationsCommandHandler(IServicePersistenceInitializer persistenceInitializer)
    : IRequestHandler<ApplySessionOperationsPersistenceMigrationsCommand>
{
    public async Task<Unit> Handle(
        ApplySessionOperationsPersistenceMigrationsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await persistenceInitializer.ApplyPendingMigrationsAsync(cancellationToken);

        return Unit.Value;
    }
}
