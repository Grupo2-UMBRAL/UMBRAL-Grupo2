using MediatR;
using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Domain.LiveSessions;
using Umbral.SessionOperations.Api.Infrastructure;

namespace Umbral.SessionOperations.Api.Application.LiveSessions;

public sealed record CreateLiveSessionCommand(
    Guid MissionId,
    string Name,
    DateTimeOffset? ScheduledStartAtUtc,
    IReadOnlyList<Guid>? SelectedMissionStageIds) : IRequest<LiveSessionResponse>
{
}

public sealed class CreateLiveSessionCommandHandler(
    SessionOperationsDbContext dbContext,
    IMissionDesignLiveSessionCatalog missionDesignLiveSessionCatalog,
    TimeProvider timeProvider)
    : IRequestHandler<CreateLiveSessionCommand, LiveSessionResponse>
{
    public async Task<LiveSessionResponse> Handle(
        CreateLiveSessionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var eligibleMission = await missionDesignLiveSessionCatalog.GetEligibleMissionForLiveSessionAsync(
            request.MissionId,
            cancellationToken);
        var sessionStageFlow = BuildSessionStageFlow(
            eligibleMission,
            request.SelectedMissionStageIds);
        var liveSession = LiveSession.Create(
            Guid.NewGuid(),
            eligibleMission.Id,
            eligibleMission.Name,
            request.Name,
            request.ScheduledStartAtUtc,
            timeProvider.GetUtcNow(),
            sessionStageFlow);

        dbContext.LiveSessions.Add(liveSession);
        await dbContext.SaveChangesAsync(cancellationToken);

        return liveSession.ToResponse();
    }

    private static IReadOnlyList<LiveSessionStage> BuildSessionStageFlow(
        EligibleMissionForLiveSessionSnapshot eligibleMission,
        IReadOnlyList<Guid>? selectedMissionStageIds)
    {
        if (selectedMissionStageIds is null || selectedMissionStageIds.Count == 0)
        {
            throw new UmbralDomainException(
                "live_session_stage_flow_required",
                "Select at least one active Mission Stage for Session Stage Flow.",
                UmbralFailureCategory.Validation);
        }

        var duplicateMissionStageId = selectedMissionStageIds
            .GroupBy(missionStageId => missionStageId)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateMissionStageId is not null)
        {
            throw new UmbralDomainException(
                "live_session_stage_flow_duplicate_stage",
                $"Mission Stage '{duplicateMissionStageId.Key}' cannot appear twice in Session Stage Flow.",
                UmbralFailureCategory.Validation);
        }

        var eligibleMissionStages = eligibleMission.MissionStages.ToDictionary(missionStage => missionStage.Id);
        var sessionStageFlow = new List<LiveSessionStage>(selectedMissionStageIds.Count);

        for (var index = 0; index < selectedMissionStageIds.Count; index++)
        {
            var selectedMissionStageId = selectedMissionStageIds[index];
            if (!eligibleMissionStages.TryGetValue(selectedMissionStageId, out var missionStage))
            {
                throw new UmbralDomainException(
                    "live_session_stage_not_eligible",
                    $"Mission Stage '{selectedMissionStageId}' is not eligible for this LiveSession.",
                    UmbralFailureCategory.Validation);
            }

            sessionStageFlow.Add(missionStage.ToDomain(index + 1));
        }

        return sessionStageFlow;
    }
}
