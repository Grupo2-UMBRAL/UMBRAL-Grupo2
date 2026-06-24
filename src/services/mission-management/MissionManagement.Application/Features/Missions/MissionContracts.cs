using MissionManagement.Domain.Missions;
using Umbral.ServiceDefaults;

namespace MissionManagement.Application.Features.Missions;

// ---------------------------------------------------------------------------
// Item kind discriminator
// ---------------------------------------------------------------------------

public static class MissionItemKind
{
    public const string Section = "Section";
    public const string Challenge = "Challenge";
}

// ---------------------------------------------------------------------------
// Write contracts (admin CRUD)
// ---------------------------------------------------------------------------

public sealed record CreateMissionRequest(
    string Name,
    string Description,
    int MaximumDurationMinutes,
    IReadOnlyList<MissionItemRequest>? Items = null);

public sealed record UpdateMissionRequest(
    string Name,
    string Description,
    int MaximumDurationMinutes,
    IReadOnlyList<MissionItemRequest>? Items = null);

public sealed record MissionItemRequest(
    string Kind = MissionItemKind.Section,
    Guid? Id = null,
    int Order = 0,
    string? Title = null,
    // Section
    IReadOnlyList<MissionItemRequest>? Children = null,
    // Challenge
    string? GameType = null,
    string? Difficulty = null,
    int? TimeLimitMinutes = null,
    bool IsActive = true,
    IReadOnlyList<QuestionRequest>? Questions = null,
    IReadOnlyList<SearchRequest>? Searches = null);

public sealed record QuestionRequest(
    Guid? Id,
    int Order,
    string Text,
    string? DifficultyOverride,
    int? TimeLimitMinutesOverride,
    IReadOnlyList<ChoiceRequest> Choices);

public sealed record ChoiceRequest(
    Guid? Id,
    int Order,
    string Text,
    bool IsCorrect);

public sealed record SearchRequest(
    Guid? Id,
    int Order,
    string Clue,
    string ExpectedQrHash,
    string? DifficultyOverride,
    int? TimeLimitMinutesOverride,
    IReadOnlyList<HintRequest>? Hints);

public sealed record HintRequest(
    Guid? Id,
    int Order,
    string Content,
    bool IsSolution,
    double? Latitude,
    double? Longitude);

// ---------------------------------------------------------------------------
// Read contracts (admin authors and sees correct answers — ADR §9)
// ---------------------------------------------------------------------------

public sealed record MissionResponse(
    Guid Id,
    string Name,
    string Description,
    int MaximumDurationMinutes,
    bool IsActive,
    IReadOnlyList<MissionItemResponse> Items);

public sealed record MissionSummaryResponse(
    Guid Id,
    string Name,
    int MaximumDurationMinutes,
    bool IsActive);

public sealed record MissionItemResponse(
    string Kind,
    Guid Id,
    int Order,
    string? Title,
    // Section
    IReadOnlyList<MissionItemResponse>? Children,
    // Challenge
    string? GameType,
    string? Difficulty,
    int? TimeLimitMinutes,
    bool? IsActive,
    IReadOnlyList<QuestionResponse>? Questions,
    IReadOnlyList<SearchResponse>? Searches);

public sealed record QuestionResponse(
    Guid Id,
    int Order,
    string Text,
    string? DifficultyOverride,
    int? TimeLimitMinutesOverride,
    IReadOnlyList<ChoiceResponse> Choices);

public sealed record ChoiceResponse(
    Guid Id,
    int Order,
    string Text,
    bool IsCorrect);

public sealed record SearchResponse(
    Guid Id,
    int Order,
    string Clue,
    string ExpectedQrHash,
    string? DifficultyOverride,
    int? TimeLimitMinutesOverride,
    IReadOnlyList<HintResponse> Hints);

public sealed record HintResponse(
    Guid Id,
    int Order,
    string Content,
    bool IsSolution,
    double? Latitude,
    double? Longitude);

// ---------------------------------------------------------------------------
// Eligible-for-live-session contracts (server-to-server; consumed by session-management)
// ---------------------------------------------------------------------------

public sealed record EligibleMissionForLiveSessionSummaryResponse(
    Guid Id,
    string Name,
    int MaximumDurationMinutes,
    string? GameType,
    int ActivePlayCount);

public sealed record EligibleMissionForLiveSessionResponse(
    Guid Id,
    string Name,
    string Description,
    int MaximumDurationMinutes,
    IReadOnlyList<EligiblePlayResponse> Plays);

public sealed record EligiblePlayResponse(
    Guid Id,
    int Order,
    string GameType,
    string Difficulty,
    int TimeLimitMinutes,
    string Prompt,
    IReadOnlyList<EligibleChoiceResponse>? Choices,
    Guid? CorrectChoiceId,
    string? ExpectedQrHash,
    IReadOnlyList<EligibleHintResponse> Hints);

// Player-facing choice: NEVER carries the correct flag.
public sealed record EligibleChoiceResponse(Guid Id, string Text);

public sealed record EligibleHintResponse(string Content, bool IsSolution, double? Latitude, double? Longitude);

// ---------------------------------------------------------------------------
// Mappings
// ---------------------------------------------------------------------------

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
