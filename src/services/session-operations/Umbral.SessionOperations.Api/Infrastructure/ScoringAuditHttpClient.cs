using System.Net.Http.Json;
using Umbral.SessionOperations.Api.Application.Scoring;

namespace Umbral.SessionOperations.Api.Infrastructure;

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
}
