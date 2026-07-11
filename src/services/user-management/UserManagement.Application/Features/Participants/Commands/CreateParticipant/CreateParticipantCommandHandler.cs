using UserManagement.Application.Abstractions;
using MediatR;
using Umbral.ServiceDefaults;
using UserManagement.Application.Common.Dtos;
using UserManagement.Application.Common.Handlers;

using Microsoft.Extensions.Logging;

namespace UserManagement.Application.Features.Participants.Commands.CreateParticipant;

public sealed class CreateParticipantCommandHandler(
    UserCreationFlowHandler flowHandler,
    IEmailNotificationService emailService,
    ILogger<CreateParticipantCommandHandler> logger)
    : IRequestHandler<CreateParticipantCommand, ParticipantDto>
{
    public async Task<ParticipantDto> Handle(CreateParticipantCommand request, CancellationToken cancellationToken)
    {
        var participantUser = await flowHandler.CreateUserAsync(
            request.Email!,
            request.Username!,
            request.Username!,
            "Jugador",
            request.Password!,
            isOperator: false,
            cancellationToken);

        var normalizedEmail = request.Email!.Trim().ToLowerInvariant();
        var normalizedUsername = request.Username!.Trim();

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

        return new ParticipantDto(participantUser.Id, participantUser.Username);
    }
}
