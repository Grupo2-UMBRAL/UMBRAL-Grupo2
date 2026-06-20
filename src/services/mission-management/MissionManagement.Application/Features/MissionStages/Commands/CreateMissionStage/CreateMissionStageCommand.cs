using MediatR;

namespace MissionManagement.Application.Features.MissionStages.Commands.CreateMissionStage;

public sealed record CreateMissionStageCommand(
    Guid MissionId,
    string Name,
    int Order,
    string Difficulty,
    string GameType,
    string? ExpectedQrHash,
    string? TriviaValidationCriteria) : IRequest<MissionStageResponse>
{
}

