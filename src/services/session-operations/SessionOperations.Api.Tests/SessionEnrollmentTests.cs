using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Umbral.ServiceDefaults;
using SessionOperations.Application.Features.LiveSessions;
using SessionOperations.Application.Features.SessionEnrollment;
using SessionOperations.Domain.LiveSessions;
using SessionOperations.Infrastructure.Persistence;
using Xunit;

namespace SessionOperations.Api.Tests;

public sealed class SessionEnrollmentDomainTests
{
    [Fact]
    public void JoinCode_Parse_NormalizesValidCode()
    {
        var joinCode = JoinCode.Parse(" abc234 ");

        Assert.Equal("ABC234", joinCode.Value);
    }

    [Theory]
    [InlineData("ABC12")]
    [InlineData("ABC1234")]
    [InlineData("ABC10O")]
    public void JoinCode_Parse_RejectsInvalidCode(string value)
    {
        var exception = Assert.Throws<UmbralDomainException>(() => JoinCode.Parse(value));

        Assert.Equal(UmbralFailureCategory.Validation, exception.Category);
    }

    [Fact]
    public void RegisterTeam_RequiresActiveEnrollmentWindow()
    {
        var liveSession = CreateLiveSession();
        var joinCode = JoinCode.Parse("ABC234");
        liveSession.AssignJoinCode(joinCode);

        var exception = Assert.Throws<UmbralDomainException>(() => liveSession.RegisterTeam(
            Guid.NewGuid(),
            "Team Cave",
            "participant-1",
            joinCode,
            new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero)));

        Assert.Equal("live_session_enrollment_window_not_active", exception.Code);
        Assert.Equal(UmbralFailureCategory.Conflict, exception.Category);
    }

    [Fact]
    public void RegisterTeam_CreatesTeamAndParticipation_WhenWindowIsOpen()
    {
        var liveSession = CreateLiveSession();
        var joinCode = JoinCode.Parse("ABC234");
        var nowUtc = new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero);
        liveSession.AssignJoinCode(joinCode);
        liveSession.OpenEnrollmentWindow(nowUtc);

        var team = liveSession.RegisterTeam(Guid.NewGuid(), "Team Cave", "participant-1", joinCode, nowUtc);

        Assert.Equal("Team Cave", team.Name);
        Assert.Single(liveSession.SessionTeams);
        var participation = Assert.Single(liveSession.TeamParticipations);
        Assert.Equal(team.Id, participation.SessionTeamId);
        Assert.Equal("participant-1", participation.ParticipantUserId);
    }

    private static LiveSession CreateLiveSession()
    {
        return LiveSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Night Mission",
            "Session Alpha",
            scheduledStartAtUtc: null,
            createdAtUtc: new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero),
            sessionStageFlow:
            [
                LiveSessionStage.Create(
                    Guid.NewGuid(),
                    "Stage 1",
                    1,
                    1,
                    30,
                    "Medium",
                    "Trivia",
                    "What symbol completes the mural?")
            ]);
    }
}

public sealed class SessionEnrollmentEndpointTests
{
    [Fact]
    public async Task RegisterTeamEndpoint_CompletesJoinCodeWindowAndTeamFlow()
    {
        await using var factory = new SessionOperationsApiFactory();
        var missionId = Guid.NewGuid();
        var missionStageId = Guid.NewGuid();
        factory.SetEligibleMission(CreateEligibleMission(missionId, missionStageId));
        var operatorClient = factory.CreateOperatorClient();
        var participantClient = factory.CreateParticipantClient("participant-99");

        var liveSession = await CreateLiveSessionAsync(operatorClient, missionId, missionStageId);
        var joinCode = await GenerateJoinCodeAsync(operatorClient, liveSession.Id);
        var windowResponse = await operatorClient.PostAsync(
            $"/api/session-operations/live-sessions/{liveSession.Id}/session-enrollment/window/open",
            content: null);
        windowResponse.EnsureSuccessStatusCode();

        var response = await participantClient.PostAsJsonAsync(
            "/api/session-operations/session-enrollment/teams",
            new RegisterTeamRequest(joinCode, "Team Cave"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RegisterTeamResponse>();
        Assert.NotNull(result);
        Assert.Equal("Team Cave", result.TeamName);
        Assert.Equal("participant-99", result.ParticipantUserId);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SessionOperationsDbContext>();
        var storedSession = await dbContext.LiveSessions
            .Include(session => session.SessionTeams)
            .Include(session => session.TeamParticipations)
            .SingleAsync(session => session.Id == liveSession.Id);
        Assert.Single(storedSession.SessionTeams);
        Assert.Single(storedSession.TeamParticipations);
    }

    [Fact]
    public async Task RegisterTeamEndpoint_ReturnsConflict_ForDuplicateTeamName()
    {
        await using var factory = new SessionOperationsApiFactory();
        var missionId = Guid.NewGuid();
        var missionStageId = Guid.NewGuid();
        factory.SetEligibleMission(CreateEligibleMission(missionId, missionStageId));
        var operatorClient = factory.CreateOperatorClient();
        var participantClient = factory.CreateParticipantClient("participant-77");

        var liveSession = await CreateLiveSessionAsync(operatorClient, missionId, missionStageId);
        var joinCode = await GenerateJoinCodeAsync(operatorClient, liveSession.Id);
        var windowResponse = await operatorClient.PostAsync(
            $"/api/session-operations/live-sessions/{liveSession.Id}/session-enrollment/window/open",
            content: null);
        windowResponse.EnsureSuccessStatusCode();

        var firstResponse = await participantClient.PostAsJsonAsync(
            "/api/session-operations/session-enrollment/teams",
            new RegisterTeamRequest(joinCode, "Team Cave"));
        firstResponse.EnsureSuccessStatusCode();

        var duplicateResponse = await participantClient.PostAsJsonAsync(
            "/api/session-operations/session-enrollment/teams",
            new RegisterTeamRequest(joinCode, " team cave "));

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    private static async Task<LiveSessionResponse> CreateLiveSessionAsync(
        HttpClient client,
        Guid missionId,
        Guid missionStageId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/session-operations/live-sessions",
            new CreateLiveSessionRequest(missionId, "Session Alpha", null, [missionStageId]));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LiveSessionResponse>())!;
    }

    private static async Task<string> GenerateJoinCodeAsync(HttpClient client, Guid liveSessionId)
    {
        var response = await client.PostAsync(
            $"/api/session-operations/live-sessions/{liveSessionId}/session-enrollment/join-code",
            content: null);
        response.EnsureSuccessStatusCode();
        var joinCode = await response.Content.ReadFromJsonAsync<GenerateJoinCodeResponse>();
        return joinCode!.JoinCode;
    }

    private static EligibleMissionForLiveSessionSnapshot CreateEligibleMission(Guid missionId, Guid missionStageId)
    {
        return new EligibleMissionForLiveSessionSnapshot(
            missionId,
            "Night Mission",
            [
                new EligibleMissionStageSnapshot(
                    missionStageId,
                    "Stage 1",
                    1,
                    1,
                    30,
                    "Medium",
                    "Trivia",
                    "What symbol completes the mural?",
                    null,
                    "answer",
                    null,
                    [])
            ]);
    }
}
