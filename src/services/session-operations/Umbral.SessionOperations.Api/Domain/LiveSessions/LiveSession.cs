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

    public string? JoinCodeValue { get; private set; }

    public DateTimeOffset? EnrollmentWindowOpenedAtUtc { get; private set; }

    public DateTimeOffset? EnrollmentWindowClosedAtUtc { get; private set; }

    public ICollection<SessionTeam> SessionTeams { get; private set; } = new List<SessionTeam>();

    public ICollection<TeamParticipation> TeamParticipations { get; private set; } = new List<TeamParticipation>();

    [JsonIgnore]
    [NotMapped]
    public IReadOnlyList<LiveSessionStage> SessionStageFlow => DeserializeSessionStageFlow(SessionStageFlowJson);

    [JsonIgnore]
    [NotMapped]
    public EnrollmentWindow EnrollmentWindow => new(EnrollmentWindowOpenedAtUtc, EnrollmentWindowClosedAtUtc);

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

    public void AssignJoinCode(JoinCode joinCode)
    {
        ArgumentNullException.ThrowIfNull(joinCode);

        if (JoinCodeValue is null)
        {
            JoinCodeValue = joinCode.Value;
            return;
        }

        if (string.Equals(JoinCodeValue, joinCode.Value, StringComparison.Ordinal))
        {
            return;
        }

        throw new UmbralDomainException(
            "live_session_join_code_already_assigned",
            "LiveSession already has a Join Code assigned.",
            UmbralFailureCategory.Conflict);
    }

    public void OpenEnrollmentWindow(DateTimeOffset openedAtUtc)
    {
        EnsureScheduled();

        if (JoinCodeValue is null)
        {
            throw new UmbralDomainException(
                "live_session_join_code_required_before_enrollment",
                "A Join Code must be generated before opening enrollment.",
                UmbralFailureCategory.Validation);
        }

        if (EnrollmentWindowClosedAtUtc is not null)
        {
            throw new UmbralDomainException(
                "live_session_enrollment_window_closed",
                "Enrollment window is already closed.",
                UmbralFailureCategory.Conflict);
        }

        if (EnrollmentWindowOpenedAtUtc is not null)
        {
            return;
        }

        EnrollmentWindowOpenedAtUtc = openedAtUtc;
    }

    public void CloseEnrollmentWindow(DateTimeOffset closedAtUtc)
    {
        if (EnrollmentWindowOpenedAtUtc is null)
        {
            throw new UmbralDomainException(
                "live_session_enrollment_window_not_opened",
                "Enrollment window has not been opened.",
                UmbralFailureCategory.Validation);
        }

        if (EnrollmentWindowClosedAtUtc is not null)
        {
            return;
        }

        if (closedAtUtc < EnrollmentWindowOpenedAtUtc)
        {
            throw new UmbralDomainException(
                "live_session_enrollment_window_close_time_invalid",
                "Enrollment window cannot close before it opens.",
                UmbralFailureCategory.Validation);
        }

        EnrollmentWindowClosedAtUtc = closedAtUtc;
    }

    public SessionTeam RegisterTeam(
        Guid sessionTeamId,
        string teamName,
        string participantUserId,
        JoinCode presentedJoinCode,
        DateTimeOffset registeredAtUtc)
    {
        EnsureEnrollmentAllowed(presentedJoinCode, registeredAtUtc);

        var normalizedTeamName = SessionTeam.NormalizeTeamName(teamName);
        if (SessionTeams.Any(team => string.Equals(team.NormalizedName, normalizedTeamName, StringComparison.Ordinal)))
        {
            throw new UmbralDomainException(
                "session_team_name_duplicate",
                "Session Team name already exists in this LiveSession.",
                UmbralFailureCategory.Conflict);
        }

        var sessionTeam = SessionTeam.Create(Id, sessionTeamId, teamName, registeredAtUtc);
        SessionTeams.Add(sessionTeam);
        EnrollParticipantInTeam(sessionTeam.Id, participantUserId, presentedJoinCode, registeredAtUtc);

        return sessionTeam;
    }

    public TeamParticipation EnrollParticipantInTeam(
        Guid sessionTeamId,
        string participantUserId,
        JoinCode presentedJoinCode,
        DateTimeOffset enrolledAtUtc)
    {
        EnsureEnrollmentAllowed(presentedJoinCode, enrolledAtUtc);

        if (SessionTeams.All(team => team.Id != sessionTeamId))
        {
            throw new UmbralDomainException(
                "session_team_not_found",
                "Session Team does not belong to this LiveSession.",
                UmbralFailureCategory.NotFound);
        }

        var normalizedParticipantUserId = ParticipantUserId.Parse(participantUserId);
        var existingParticipation = TeamParticipations.FirstOrDefault(participation =>
            string.Equals(participation.ParticipantUserId, normalizedParticipantUserId.Value, StringComparison.Ordinal));

        if (existingParticipation is null)
        {
            var participation = TeamParticipation.Create(Id, sessionTeamId, normalizedParticipantUserId, enrolledAtUtc);
            TeamParticipations.Add(participation);
            return participation;
        }

        if (existingParticipation.SessionTeamId == sessionTeamId)
        {
            return existingParticipation;
        }

        existingParticipation.MoveToTeam(sessionTeamId, enrolledAtUtc);
        return existingParticipation;
    }

    public bool IsEnrollmentOpenAt(DateTimeOffset nowUtc) => EnrollmentWindow.IsOpenAt(nowUtc);

    private void EnsureEnrollmentAllowed(JoinCode presentedJoinCode, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(presentedJoinCode);
        EnsureScheduled();

        if (JoinCodeValue is null)
        {
            throw new UmbralDomainException(
                "live_session_join_code_not_generated",
                "LiveSession does not have a Join Code generated.",
                UmbralFailureCategory.Validation);
        }

        if (!string.Equals(JoinCodeValue, presentedJoinCode.Value, StringComparison.Ordinal))
        {
            throw new UmbralDomainException(
                "join_code_invalid_for_live_session",
                "Join Code is invalid for this LiveSession.",
                UmbralFailureCategory.NotFound);
        }

        if (!EnrollmentWindow.IsOpenAt(nowUtc))
        {
            throw new UmbralDomainException(
                "live_session_enrollment_window_not_active",
                "Enrollment window is not active.",
                UmbralFailureCategory.Conflict);
        }
    }

    private void EnsureScheduled()
    {
        if (string.Equals(State, LiveSessionStates.Scheduled, StringComparison.Ordinal))
        {
            return;
        }

        throw new UmbralDomainException(
            "live_session_not_scheduled",
            "LiveSession must be scheduled to accept enrollment changes.",
            UmbralFailureCategory.Conflict);
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
