using UserManagement.Application.Abstractions;
using MediatR;
using Umbral.ServiceDefaults;
using UserManagement.Application.Common.Dtos;
using UserManagement.Application.Common.Mappings;

using Microsoft.Extensions.Logging;

namespace UserManagement.Application.Features.Participants.Commands.CreateParticipant;

public sealed class CreateParticipantCommandHandler(
    IOperatorAdministrationPort port,
    IEmailNotificationService emailService,
    ILogger<CreateParticipantCommandHandler> logger)
    : IRequestHandler<CreateParticipantCommand, ParticipantDto>
{
    public async Task<ParticipantDto> Handle(CreateParticipantCommand request, CancellationToken cancellationToken)
    {
        // 1. Validation
        CreateParticipantCommandValidator.Validate(request);

        var normalizedEmail = request.Email!.Trim().ToLowerInvariant();
        var normalizedUsername = request.Username!.Trim();

        // 2. Uniqueness checks (same guarantees as operator creation)
        var usersWithSameEmail = await port.FindUsersByEmailAsync(normalizedEmail, cancellationToken);
        if (usersWithSameEmail.Count > 0)
        {
            throw new UmbralDomainException(
                "participant_email_duplicate",
                "Email already belongs to another User.",
                UmbralFailureCategory.Conflict);
        }

        var usersWithSameUsername = await port.FindUsersByUsernameAsync(normalizedUsername, cancellationToken);
        if (usersWithSameUsername.Count > 0)
        {
            throw new UmbralDomainException(
                "participant_username_duplicate",
                "Username already belongs to another User.",
                UmbralFailureCategory.Conflict);
        }

        // 3. Execution (Keycloak Creation via Infrastructure port). Participants self-register with
        //    only username/email/password, so the display names are synthesized from the username.
        var createdUser = await port.CreateUserAsync(
            normalizedUsername,
            normalizedEmail,
            normalizedUsername,
            "Jugador",
            request.Password!.Trim(),
            cancellationToken);

        try
        {
            await port.AssignParticipantRoleAsync(createdUser.Id, cancellationToken);
        }
        catch
        {
            try
            {
                await port.DeleteUserAsync(createdUser.Id, cancellationToken);
            }
            catch { }
            throw;
        }

        var participantUser = await port.GetUserByIdAsync(createdUser.Id, cancellationToken);

        if (participantUser is null)
        {
            throw new UmbralTechnicalException(
                "participant_user_recovery_failed",
                "Keycloak created the User but did not return it afterwards.");
        }

        try
        {
            await emailService.SendParticipantWelcomeAsync(
                normalizedEmail,
                normalizedUsername,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send welcome email to participant {Email}.", normalizedEmail);
        }

        return participantUser.ToParticipantDto();
    }
}
