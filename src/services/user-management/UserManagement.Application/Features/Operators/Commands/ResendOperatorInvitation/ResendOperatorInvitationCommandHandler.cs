using UserManagement.Application.Abstractions;
using MediatR;
using Umbral.ServiceDefaults;
using UserManagement.Application.Common.Dtos;

namespace UserManagement.Application.Features.Operators.Commands.ResendOperatorInvitation;

public sealed class ResendOperatorInvitationCommandHandler(IOperatorAdministrationPort port)
    : IRequestHandler<ResendOperatorInvitationCommand, OperatorDto>
{
    public async Task<OperatorDto> Handle(ResendOperatorInvitationCommand request, CancellationToken cancellationToken)
    {
        var normalizedUserId = request.UserId.Trim();

        // Only re-invite a real Operator: the list is scoped to the Operator role, so an arbitrary realm
        // user id resolves to nothing here and is rejected rather than emailed an onboarding link.
        var operators = await port.ListOperatorsAsync(cancellationToken);
        var operatorUser = operators.FirstOrDefault(candidate => candidate.Id == normalizedUserId);

        if (operatorUser is null)
        {
            throw new UmbralDomainException(
                "operator_user_not_found",
                "Operator User was not found.",
                UmbralFailureCategory.NotFound);
        }

        // Unlike creation, a failure here surfaces: the Administrator asked to resend, so they should be
        // told if Keycloak refused rather than shown a false success.
        await port.SendOperatorOnboardingInvitationAsync(normalizedUserId, cancellationToken);

        return operatorUser;
    }
}
