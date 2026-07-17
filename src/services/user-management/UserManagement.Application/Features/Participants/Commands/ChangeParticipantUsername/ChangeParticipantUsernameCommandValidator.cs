using FluentValidation;
using UserManagement.Application.Common;

namespace UserManagement.Application.Features.Participants.Commands.ChangeParticipantUsername;

/// <summary>
/// Fail-fast like its registration counterpart, and deliberately built from the same shared rule so
/// both paths accept exactly the same handles under the same <c>participant_username_*</c> codes.
/// </summary>
public sealed class ChangeParticipantUsernameCommandValidator
    : AbstractValidator<ChangeParticipantUsernameCommand>
{
    public ChangeParticipantUsernameCommandValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.Username).ParticipantUsername();
    }
}
