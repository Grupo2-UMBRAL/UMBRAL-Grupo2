using System.Net.Http.Json;
using SessionOperations.Application.Scoring;

namespace SessionOperations.Infrastructure;

public sealed class ScoringAuditHttpClient(HttpClient httpClient) : IScoringAuditClient
{
    public async Task RecordStageCreditAsync(
        RecordStageCreditRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await httpClient.PostAsJsonAsync(
            $"/api/scoring-audit/sessions/{request.LiveSessionId}/scores",
            new
            {
                request.SessionTeamId,
                request.MissionStageId,
                request.Difficulty,
                request.ResolutionTime,
                request.RecordedAt,
                request.ValidationOverride
            },
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"Scoring Audit rejected Stage Credit recording with status {(int)response.StatusCode}: {detail}",
            null,
            response.StatusCode);
    }

    public async Task<ApplyPenaltyResponse> ApplyPenaltyAsync(
        ApplyPenaltyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await httpClient.PostAsJsonAsync(
            $"/api/scoring-audit/sessions/{request.LiveSessionId}/penalties",
            new
            {
                request.SessionTeamId,
                request.CommandId,
                request.Severity,
                request.AppliedByOperatorUserId,
                request.Reason,
                request.RecordedAt
            },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Scoring Audit rejected Penalty recording with status {(int)response.StatusCode}: {detail}",
                null,
                response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<ApplyPenaltyResponse>(cancellationToken);
        return payload
            ?? throw new HttpRequestException("Scoring Audit returned an empty Penalty response.");
    }

    public async Task LogSessionEventAsync(
        Guid liveSessionId,
        string eventType,
        string description,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"/api/scoring-audit/sessions/{liveSessionId}/event-log",
            new
            {
                EventType = eventType,
                Description = description
            },
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"Scoring Audit rejected Session Event Log recording with status {(int)response.StatusCode}: {detail}",
            null,
            response.StatusCode);
    }
}
