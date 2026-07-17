using MediatR;
using Umbral.ServiceDefaults;
using UserManagement.Application.Abstractions;
using UserManagement.Application.Common.Dtos;

namespace UserManagement.Application.Features.Operators.Commands.SendOperatorPasswordResetLink;

public sealed class SendOperatorPasswordResetLinkCommandHandler(IOperatorAdministrationPort port)
    : IRequestHandler<SendOperatorPasswordResetLinkCommand, OperatorDto>
{
    public async Task<OperatorDto> Handle(SendOperatorPasswordResetLinkCommand request, CancellationToken cancellationToken)
    {
        var userId = request.UserId.Trim();
        var operatorUser = (await port.ListOperatorsAsync(cancellationToken))
            .FirstOrDefault(candidate => candidate.Id == userId);

        if (operatorUser is null)
        {
            throw new UmbralDomainException("operator_user_not_found", "Operator User was not found.", UmbralFailureCategory.NotFound);
        }

        await port.SendOperatorPasswordResetAsync(userId, cancellationToken);
        return operatorUser;
    }
}
