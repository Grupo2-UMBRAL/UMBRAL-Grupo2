using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Umbral.ServiceDefaults;
using SessionOperations.Application.Features.LiveSessions;

namespace SessionOperations.Infrastructure;

public sealed class MissionManagementLiveSessionCatalog(HttpClient httpClient) : IMissionManagementLiveSessionCatalog
{
    public async Task<EligibleMissionForLiveSessionSnapshot> GetEligibleMissionForLiveSessionAsync(
        Guid missionId,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(
            $"api/mission-management/missions/eligible-for-live-session/{missionId}",
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateUpstreamExceptionAsync(response, missionId, cancellationToken);
        }

        var mission = await response.Content.ReadFromJsonAsync<EligibleMissionForLiveSessionSnapshot>(
            cancellationToken: cancellationToken);
        if (mission is null)
        {
            throw new UmbralTechnicalException(
                "mission_management_live_session_catalog_empty_payload",
                $"Mission Management returned an empty payload for Mission '{missionId}'.");
        }

        return mission;
    }

    private static async Task<UmbralServiceException> CreateUpstreamExceptionAsync(
        HttpResponseMessage response,
        Guid missionId,
        CancellationToken cancellationToken)
    {
        var detail = $"Mission Management request failed for Mission '{missionId}'.";
        string? code = null;
        UmbralFailureCategory? category = null;

        try
        {
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var problemDetails = await JsonSerializer.DeserializeAsync<MissionManagementProblemDetails>(
                contentStream,
                cancellationToken: cancellationToken);

            if (problemDetails is not null)
            {
                if (!string.IsNullOrWhiteSpace(problemDetails.Detail))
                {
                    detail = problemDetails.Detail;
                }

                code = problemDetails.Code;
                if (Enum.TryParse<UmbralFailureCategory>(problemDetails.Category, true, out var parsedCategory))
                {
                    category = parsedCategory;
                }
            }
        }
        catch (JsonException)
        {
        }

        var failureCode = string.IsNullOrWhiteSpace(code)
            ? $"mission_management_live_session_catalog_{(int)response.StatusCode}"
            : code;
        var failureCategory = category ?? MapFailureCategory(response.StatusCode);

        return response.StatusCode >= HttpStatusCode.InternalServerError
            ? new UmbralTechnicalException(failureCode, detail)
            : new UmbralDomainException(failureCode, detail, failureCategory);
    }

    private static UmbralFailureCategory MapFailureCategory(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.BadRequest => UmbralFailureCategory.Validation,
            HttpStatusCode.Unauthorized => UmbralFailureCategory.Unauthorized,
            HttpStatusCode.Forbidden => UmbralFailureCategory.Forbidden,
            HttpStatusCode.NotFound => UmbralFailureCategory.NotFound,
            HttpStatusCode.Conflict => UmbralFailureCategory.Conflict,
            HttpStatusCode.UnprocessableEntity => UmbralFailureCategory.Domain,
            _ => UmbralFailureCategory.Technical
        };

    private sealed record MissionManagementProblemDetails(
        string? Detail,
        string? Code,
        string? Category);
}
