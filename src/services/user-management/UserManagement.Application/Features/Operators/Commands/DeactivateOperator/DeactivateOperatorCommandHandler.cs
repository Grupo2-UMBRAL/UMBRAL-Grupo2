using UserManagement.Application.Abstractions;
using MediatR;
using Umbral.ServiceDefaults;
using UserManagement.Domain.Entities;

namespace UserManagement.Application.Features.Operators.Commands.DeactivateOperator;

public sealed class DeactivateOperatorCommandHandler(IOperatorAdministrationPort port)
    : IRequestHandler<DeactivateOperatorCommand, OperatorUser>
{
    public async Task<OperatorUser> Handle(DeactivateOperatorCommand request, CancellationToken cancellationToken)
    {
        var normalizedUserId = request.UserId?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedUserId))
        {
            throw new UmbralDomainException(
                "operator_user_id_required",
                "Operator user id is required.",
                UmbralFailureCategory.Validation);
        }

        var operators = await port.ListOperatorsAsync(cancellationToken);
        var operatorUser = operators.FirstOrDefault(candidate => candidate.Id == normalizedUserId);

        if (operatorUser is null)
        {
            throw new UmbralDomainException(
                "operator_user_not_found",
                "Operator User was not found.",
                UmbralFailureCategory.NotFound);
        }

        if (!operatorUser.IsActive)
        {
            return operatorUser;
        }

        return await port.SetUserEnabledAsync(normalizedUserId, enabled: false, cancellationToken);
    }
}

