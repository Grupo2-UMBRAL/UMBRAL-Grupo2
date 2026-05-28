using MediatR;
using Umbral.ServiceDefaults;

namespace Umbral.MissionDesign.Api.Application.Bootstrap.Commands;

public sealed record ApplyMissionDesignPersistenceMigrationsCommand : IRequest;

public sealed class ApplyMissionDesignPersistenceMigrationsCommandHandler(IServicePersistenceInitializer persistenceInitializer)
    : IRequestHandler<ApplyMissionDesignPersistenceMigrationsCommand>
{
    public async Task<Unit> Handle(
        ApplyMissionDesignPersistenceMigrationsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await persistenceInitializer.ApplyPendingMigrationsAsync(cancellationToken);

        return Unit.Value;
    }
}
