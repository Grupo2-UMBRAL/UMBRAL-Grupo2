using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using Umbral.ServiceDefaults;

namespace Umbral.SessionOperations.Api.Domain.LiveSessions;

public sealed class LiveSession
{
    private static readonly JsonSerializerOptions SessionStageFlowSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private LiveSession()
    {
    }

    private LiveSession(
        Guid id,
        Guid missionId,
        string missionName,
        string name,
        string state,
        DateTimeOffset? scheduledStartAtUtc,
        DateTimeOffset createdAtUtc,
        string sessionStageFlowJson)
    {
        Id = id;
        MissionId = missionId;
        MissionName = missionName;
        Name = name;
        State = state;
        ScheduledStartAtUtc = scheduledStartAtUtc;
        CreatedAtUtc = createdAtUtc;
        SessionStageFlowJson = sessionStageFlowJson;
    }

    public Guid Id { get; private set; }

    public Guid MissionId { get; private set; }

    public string MissionName { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string State { get; private set; } = string.Empty;

    public DateTimeOffset? ScheduledStartAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public string SessionStageFlowJson { get; private set; } = "[]";

    [JsonIgnore]
    [NotMapped]
    public IReadOnlyList<LiveSessionStage> SessionStageFlow => DeserializeSessionStageFlow(SessionStageFlowJson);

    public static LiveSession Create(
        Guid id,
        Guid missionId,
        string missionName,
        string name,
        DateTimeOffset? scheduledStartAtUtc,
        DateTimeOffset createdAtUtc,
        IReadOnlyList<LiveSessionStage> sessionStageFlow)
    {
        var liveSession = new LiveSession(
            id == Guid.Empty ? Guid.NewGuid() : id,
            NormalizeGuid(missionId, "live_session_mission_required", "LiveSession must reference a Mission."),
            NormalizeRequiredText(missionName, "live_session_mission_name_required", "Mission name is required.", 120),
            NormalizeRequiredText(name, "live_session_name_required", "LiveSession name is required.", 120),
            LiveSessionStates.Scheduled,
            scheduledStartAtUtc,
            createdAtUtc,
            "[]");

        liveSession.ReplaceSessionStageFlow(sessionStageFlow);

        return liveSession;
    }

    public void ReplaceSessionStageFlow(IReadOnlyList<LiveSessionStage> sessionStageFlow)
    {
        ArgumentNullException.ThrowIfNull(sessionStageFlow);

        var normalizedSessionStageFlow = NormalizeSessionStageFlow(sessionStageFlow);
        SessionStageFlowJson = JsonSerializer.Serialize(normalizedSessionStageFlow, SessionStageFlowSerializerOptions);
    }

    private static Guid NormalizeGuid(Guid value, string errorCode, string errorMessage)
    {
        if (value == Guid.Empty)
        {
            throw new UmbralDomainException(errorCode, errorMessage, UmbralFailureCategory.Validation);
        }

        return value;
    }

    private static string NormalizeRequiredText(
        string value,
        string errorCode,
        string errorMessage,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UmbralDomainException(errorCode, errorMessage, UmbralFailureCategory.Validation);
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new UmbralDomainException(
                $"{errorCode}_too_long",
                $"Value cannot exceed {maximumLength} characters.",
                UmbralFailureCategory.Validation);
        }

        return normalized;
    }

    private static IReadOnlyList<LiveSessionStage> NormalizeSessionStageFlow(IReadOnlyList<LiveSessionStage> sessionStageFlow)
    {
        if (sessionStageFlow.Count == 0)
        {
            throw new UmbralDomainException(
                "live_session_stage_flow_required",
                "LiveSession must include at least one active Session Stage.",
                UmbralFailureCategory.Validation);
        }

        var duplicateMissionStageId = sessionStageFlow
            .GroupBy(stage => stage.MissionStageId)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateMissionStageId is not null)
        {
            throw new UmbralDomainException(
                "live_session_stage_flow_duplicate_stage",
                $"Mission Stage '{duplicateMissionStageId.Key}' cannot appear twice in Session Stage Flow.",
                UmbralFailureCategory.Validation);
        }

        var orderedStages = sessionStageFlow
            .OrderBy(stage => stage.SessionStageOrder)
            .ToArray();

        for (var index = 0; index < orderedStages.Length; index++)
        {
            if (orderedStages[index].SessionStageOrder != index + 1)
            {
                throw new UmbralDomainException(
                    "live_session_stage_flow_order_invalid",
                    "Session Stage Flow order must be contiguous and start at one.",
                    UmbralFailureCategory.Validation);
            }
        }

        return orderedStages;
    }

    private static IReadOnlyList<LiveSessionStage> DeserializeSessionStageFlow(string? sessionStageFlowJson)
    {
        if (string.IsNullOrWhiteSpace(sessionStageFlowJson))
        {
            return Array.Empty<LiveSessionStage>();
        }

        var sessionStageFlow =
            JsonSerializer.Deserialize<List<LiveSessionStage>>(sessionStageFlowJson, SessionStageFlowSerializerOptions);

        return sessionStageFlow is null
            ? Array.Empty<LiveSessionStage>()
            : sessionStageFlow;
    }
}
