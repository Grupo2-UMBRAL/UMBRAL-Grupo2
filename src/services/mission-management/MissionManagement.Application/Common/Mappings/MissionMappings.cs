using MissionManagement.Application.Common.Dtos;
using MissionManagement.Domain.Missions;
using Umbral.ServiceDefaults;

namespace MissionManagement.Application.Common.Mappings;

/// <summary>
/// Hand-written mappings between the Mission aggregate and the request/response contracts. Kept
/// explicit (no AutoMapper/Mapster) so the projection — including the player-safe eligible views that
/// must never leak the correct answer — is trivially traceable.
/// </summary>
public static class MissionMappings
{
    public static MissionResponse ToResponse(this Mission mission)
    {
        ArgumentNullException.ThrowIfNull(mission);

        return new MissionResponse(
            mission.Id,
            mission.Name,
            mission.Description,
            mission.MaximumDurationMinutes,
            mission.IsActive,
            mission.RootItems.Select(ToItemResponse).ToArray());
    }

    public static MissionSummaryResponse ToSummaryResponse(this Mission mission)
    {
        ArgumentNullException.ThrowIfNull(mission);

        return new MissionSummaryResponse(
            mission.Id,
            mission.Name,
            mission.MaximumDurationMinutes,
            mission.IsActive);
    }

    public static EligibleMissionForLiveSessionSummaryResponse ToEligibleForLiveSessionSummaryResponse(this Mission mission)
    {
        ArgumentNullException.ThrowIfNull(mission);

        var plays = mission.Flatten();

        return new EligibleMissionForLiveSessionSummaryResponse(
            mission.Id,
            mission.Name,
            mission.MaximumDurationMinutes,
            null,
            plays.Count);
    }

    public static EligibleMissionForLiveSessionResponse ToEligibleForLiveSessionResponse(this Mission mission)
    {
        ArgumentNullException.ThrowIfNull(mission);

        var plays = mission.Flatten()
            .Select(static play => new EligiblePlayResponse(
                play.Id,
                play.Order,
                play.GameType,
                play.Difficulty,
                play.TimeLimitMinutes,
                play.Prompt,
                play.Choices.Count == 0
                    ? null
                    : play.Choices.Select(static choice => new EligibleChoiceResponse(choice.Id, choice.Text)).ToArray(),
                play.CorrectChoiceId,
                play.ExpectedQrHash,
                play.Hints.Select(static hint => new EligibleHintResponse(hint.Content, hint.IsSolution, hint.Latitude, hint.Longitude)).ToArray()))
            .ToArray();

        return new EligibleMissionForLiveSessionResponse(
            mission.Id,
            mission.Name,
            mission.Description,
            mission.MaximumDurationMinutes,
            plays);
    }

    // --- request -> domain --------------------------------------------------

    public static IReadOnlyList<PathItem> ToDomain(
        this IReadOnlyList<MissionItemRequest> items,
        Guid missionId)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items.Select(item => item.ToDomain(missionId, parentSectionId: null)).ToArray();
    }

    private static PathItem ToDomain(this MissionItemRequest request, Guid missionId, Guid? parentSectionId)
    {
        ArgumentNullException.ThrowIfNull(request);

        var id = request.Id ?? Guid.NewGuid();

        if (string.Equals(request.Kind, MissionItemKind.Section, StringComparison.OrdinalIgnoreCase))
        {
            var children = request.Children?
                .Select(child => child.ToDomain(missionId, id))
                .ToArray() ?? Array.Empty<PathItem>();

            return Section.Create(id, missionId, parentSectionId, request.Order, request.Title ?? string.Empty, children);
        }

        if (string.Equals(request.Kind, MissionItemKind.Challenge, StringComparison.OrdinalIgnoreCase))
        {
            var plays = BuildPlays(request, id);

            return Challenge.Create(
                id,
                missionId,
                parentSectionId,
                request.Order,
                request.Title ?? string.Empty,
                request.GameType ?? string.Empty,
                request.Difficulty ?? string.Empty,
                request.TimeLimitMinutes ?? 0,
                request.IsActive,
                plays);
        }

        throw new UmbralDomainException(
            "mission_item_kind_unsupported",
            $"Mission item kind '{request.Kind}' is not supported. Use Section or Challenge.",
            UmbralFailureCategory.Validation);
    }

    private static IReadOnlyList<Play> BuildPlays(MissionItemRequest request, Guid challengeId)
    {
        var plays = new List<Play>();

        if (request.Questions is not null)
        {
            plays.AddRange(request.Questions.Select(question => question.ToDomain(challengeId)));
        }

        if (request.Searches is not null)
        {
            plays.AddRange(request.Searches.Select(search => search.ToDomain(challengeId)));
        }

        return plays;
    }

    private static Play ToDomain(this QuestionRequest request, Guid challengeId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Choices);

        var questionId = request.Id ?? Guid.NewGuid();
        var choices = request.Choices
            .Select(choice => Choice.Create(choice.Id ?? Guid.NewGuid(), questionId, choice.Order, choice.Text, choice.IsCorrect))
            .ToArray();

        return Question.Create(
            questionId,
            challengeId,
            request.Order,
            request.Text,
            request.DifficultyOverride,
            request.TimeLimitMinutesOverride,
            choices);
    }

    private static Play ToDomain(this SearchRequest request, Guid challengeId)
    {
        ArgumentNullException.ThrowIfNull(request);

        var searchId = request.Id ?? Guid.NewGuid();
        var hints = request.Hints?
            .Select(hint => Hint.Create(hint.Id ?? Guid.NewGuid(), searchId, hint.Order, hint.Content, hint.IsSolution, hint.Latitude, hint.Longitude))
            .ToArray();

        return Search.Create(
            searchId,
            challengeId,
            request.Order,
            request.Clue,
            request.ExpectedQrHash,
            request.DifficultyOverride,
            request.TimeLimitMinutesOverride,
            hints);
    }

    // --- domain -> response -------------------------------------------------

    private static MissionItemResponse ToItemResponse(this PathItem item)
    {
        return item switch
        {
            Section section => new MissionItemResponse(
                MissionItemKind.Section,
                section.Id,
                section.Order,
                section.Title,
                section.Children.Select(ToItemResponse).ToArray(),
                GameType: null,
                Difficulty: null,
                TimeLimitMinutes: null,
                IsActive: null,
                Questions: null,
                Searches: null),
            Challenge challenge => new MissionItemResponse(
                MissionItemKind.Challenge,
                challenge.Id,
                challenge.Order,
                challenge.Title,
                Children: null,
                challenge.GameType,
                challenge.DefaultDifficulty,
                challenge.DefaultTimeLimitMinutes,
                challenge.IsActive,
                challenge.Plays.OfType<Question>().Select(ToResponse).ToArray(),
                challenge.Plays.OfType<Search>().Select(ToResponse).ToArray()),
            _ => throw new UmbralDomainException(
                "mission_item_kind_unsupported",
                $"Path item '{item.Id}' has an unsupported kind.",
                UmbralFailureCategory.Validation)
        };
    }

    private static QuestionResponse ToResponse(this Question question)
    {
        return new QuestionResponse(
            question.Id,
            question.Order,
            question.Text,
            question.DifficultyOverride,
            question.TimeLimitMinutesOverride,
            question.Choices
                .OrderBy(choice => choice.Order)
                .Select(choice => new ChoiceResponse(choice.Id, choice.Order, choice.Text, choice.IsCorrect))
                .ToArray());
    }

    private static SearchResponse ToResponse(this Search search)
    {
        return new SearchResponse(
            search.Id,
            search.Order,
            search.Clue,
            search.ExpectedQrHash,
            search.DifficultyOverride,
            search.TimeLimitMinutesOverride,
            search.Hints
                .OrderBy(hint => hint.Order)
                .Select(hint => new HintResponse(hint.Id, hint.Order, hint.Content, hint.IsSolution, hint.Latitude, hint.Longitude))
                .ToArray());
    }
}
