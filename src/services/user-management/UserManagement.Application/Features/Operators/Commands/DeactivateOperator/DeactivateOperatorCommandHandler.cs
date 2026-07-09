using UserManagement.Application.Abstractions;
using MediatR;
using Umbral.ServiceDefaults;
using UserManagement.Application.Common.Dtos;
using UserManagement.Application.Common.Mappings;

namespace UserManagement.Application.Features.Operators.Commands.DeactivateOperator;

public sealed class DeactivateOperatorCommandHandler(IOperatorAdministrationPort port)
    : IRequestHandler<DeactivateOperatorCommand, OperatorDto>
{
    public async Task<OperatorDto> Handle(DeactivateOperatorCommand request, CancellationToken cancellationToken)
    {
        DeactivateOperatorCommandValidator.Validate(request);

        var normalizedUserId = request.UserId.Trim();

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
            return operatorUser.ToDto();
        }

        var disabled = await port.SetUserEnabledAsync(normalizedUserId, enabled: false, cancellationToken);
        return disabled.ToDto();
    }
}
