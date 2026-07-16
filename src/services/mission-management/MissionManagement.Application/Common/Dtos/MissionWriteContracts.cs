namespace MissionManagement.Application.Common.Dtos;

// ---------------------------------------------------------------------------
// Write contracts (admin CRUD)
// ---------------------------------------------------------------------------

/// <summary>
/// Body of the create-mission call. The Mission is always created inactive: it must be activated
/// separately, and activation is refused unless the path yields at least one play from an active,
/// well-formed Challenge.
/// </summary>
/// <param name="Name" example="Downtown treasure hunt">Must be unique across all Missions — a clash is rejected as a conflict, not de-duplicated. Trimmed; max 120 characters.</param>
/// <param name="Description" example="Two-hour run around the old town mixing QR searches with trivia stops.">Operator-facing blurb used when picking a Mission for a LiveSession. Trimmed; max 1024 characters.</param>
/// <param name="MaximumDurationMinutes" example="120">Wall-clock budget for the whole Mission run, in minutes. Must be between 1 and 1440. Unrelated to the per-play Time Limits, which are not summed or checked against it.</param>
/// <param name="Items">Ordered root Path Items of the Mission path. Null or omitted creates the Mission with an empty path — accepted, but it stays ineligible for a LiveSession until Challenges with plays are added.</param>
public sealed record CreateMissionRequest(
    string Name,
    string Description,
    int MaximumDurationMinutes,
    IReadOnlyList<MissionItemRequest>? Items = null);

/// <summary>
/// Body of the update-mission call. Every scalar is replaced with the value sent, so omitted scalars
/// are overwritten with their defaults rather than left alone. Updating an active Mission is refused
/// if the resulting path would no longer be eligible for a LiveSession.
/// </summary>
/// <param name="Name" example="Downtown treasure hunt">Must stay unique across the other Missions; the Mission being updated does not clash with itself. Trimmed; max 120 characters.</param>
/// <param name="Description" example="Two-hour run around the old town mixing QR searches with trivia stops.">Operator-facing blurb used when picking a Mission for a LiveSession. Trimmed; max 1024 characters.</param>
/// <param name="MaximumDurationMinutes" example="120">Wall-clock budget for the whole Mission run, in minutes. Must be between 1 and 1440.</param>
/// <param name="Items">Ordered root Path Items to replace the whole existing path with. Null or omitted means "leave the path untouched" and updates only the scalars above; an empty list is not the same thing — it wipes the path.</param>
public sealed record UpdateMissionRequest(
    string Name,
    string Description,
    int MaximumDurationMinutes,
    IReadOnlyList<MissionItemRequest>? Items = null);

/// <summary>
/// A Path Item of the Mission path, in either of its two shapes: an inert organizational Section
/// (which nests further Path Items) or a playable Challenge (which holds the plays). The fields of the
/// shape that does not apply are ignored — a Section never carries Game Type, Difficulty or Time Limit.
/// </summary>
/// <param name="Kind" example="Challenge">Selects the shape of this item: "Section" or "Challenge" (case-insensitive). Any other value is rejected.</param>
/// <param name="Id" example="9f3c1b7e-5a41-4d2e-9b28-6c0d4a1f8e73">Null asks the server to mint a new id. Because the path is replaced wholesale on update, reusing a known id is how an item keeps its identity across edits.</param>
/// <param name="Order" example="1">Position among siblings only — it is not a global index. Must be unique within the sibling list; ties are rejected.</param>
/// <param name="Title" example="Old town square">Required for both shapes despite being nullable here — null collapses to empty and is then rejected. Trimmed; max 120 characters. On a Section this is the item's only real content.</param>
/// <param name="Children">Nested Path Items, Sections included — Sections nest recursively. Meaningful only when Kind is "Section"; null means the Section groups nothing.</param>
/// <param name="GameType" example="Treasure Hunt">Challenge only: "Trivia" or "Treasure Hunt". Fixes which plays are legal here — the plays must all match it. Null on a Challenge fails validation.</param>
/// <param name="Difficulty" example="Medium">Challenge only: the default Difficulty ("Easy", "Medium" or "Hard") applied to every play that does not override it. Null on a Challenge fails validation.</param>
/// <param name="TimeLimitMinutes" example="5">Challenge only: the default Time Limit, in minutes, for each play that does not override it — per play, not for the Challenge as a whole. Null on a Challenge fails validation.</param>
/// <param name="IsActive">Challenge only. An inactive Challenge is kept in the path but skipped by the flatten, so its plays never reach a LiveSession.</param>
/// <param name="Questions">Trivia plays of this Challenge. Null and empty both mean "no Questions"; a Challenge left with no plays at all is still stored, but counts as malformed and is skipped by the flatten.</param>
/// <param name="Searches">Treasure Hunt plays of this Challenge. Null and empty both mean "no Searches". Sending these alongside Questions mixes Game Types and is rejected.</param>
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

/// <summary>
/// A Trivia play: a prompt with 2 to 4 Choices, exactly one of them correct. Only legal inside a
/// Challenge whose Game Type is Trivia.
/// </summary>
/// <param name="Id" example="2b8f6d10-4c93-4a57-8e21-b7d5c3f09a64">Null asks the server to mint a new id. Reuse a known id to keep this Question's identity across an update, which replaces the path wholesale.</param>
/// <param name="Order" example="1">Position among the plays of the owning Challenge only — the global play index is derived by the flatten, not taken from here.</param>
/// <param name="Text" example="Which building is the oldest in the square?">The prompt shown to participants. Free text; no correct answer is expressed here — that lives on the Choices.</param>
/// <param name="DifficultyOverride" example="Hard">Replaces the Challenge default Difficulty for this Question alone. Null means "inherit the Challenge default"; there is no deeper chain than those two levels.</param>
/// <param name="TimeLimitMinutesOverride" example="2">Replaces the Challenge default Time Limit for this Question alone. Null means "inherit the Challenge default".</param>
/// <param name="Choices">Between 2 and 4 Choices, exactly one flagged correct; anything else is rejected. Ordered by their own Order, which must have no ties.</param>
public sealed record QuestionRequest(
    Guid? Id,
    int Order,
    string Text,
    string? DifficultyOverride,
    int? TimeLimitMinutesOverride,
    IReadOnlyList<ChoiceRequest> Choices);

/// <summary>
/// One answer option of a Question. Options are always closed choices — a Question never accepts
/// free-text answers.
/// </summary>
/// <param name="Id" example="6d4a2c88-1f37-4b90-a5e6-3c8b7d2e1904">Null asks the server to mint a new id. Reuse a known id to keep this Choice's identity across an update.</param>
/// <param name="Order" example="1">Position among the sibling Choices; must be unique within the Question. Determines display order, not correctness.</param>
/// <param name="Text" example="The clock tower">The option shown to participants.</param>
/// <param name="IsCorrect">Exactly one Choice per Question must set this. This flag is authoring-only — it is never projected to participants.</param>
public sealed record ChoiceRequest(
    Guid? Id,
    int Order,
    string Text,
    bool IsCorrect);

/// <summary>
/// A Treasure Hunt play: a clue leading to an expected QR code, with its own Hints. Only legal inside
/// a Challenge whose Game Type is Treasure Hunt.
/// </summary>
/// <param name="Id" example="0a7e5b32-9d16-4c48-bf03-2e91a6d47c85">Null asks the server to mint a new id. Reuse a known id to keep this Search's identity across an update.</param>
/// <param name="Order" example="1">Position among the plays of the owning Challenge only — the global play index is derived by the flatten.</param>
/// <param name="Clue" example="Look where the water has run since 1890.">The prompt that leads participants toward the QR code. Free text.</param>
/// <param name="ExpectedQrHash" example="4f2a9c1d8b3e6057">The hash a scanned QR must match to resolve this Search. Store the hash, not the QR payload itself.</param>
/// <param name="DifficultyOverride" example="Easy">Replaces the Challenge default Difficulty for this Search alone. Null means "inherit the Challenge default".</param>
/// <param name="TimeLimitMinutesOverride" example="10">Replaces the Challenge default Time Limit for this Search alone. Null means "inherit the Challenge default".</param>
/// <param name="Hints">Help attached to this Search. Null and empty both mean "no Hints" — Hints are optional, and Trivia plays never have them.</param>
public sealed record SearchRequest(
    Guid? Id,
    int Order,
    string Clue,
    string ExpectedQrHash,
    string? DifficultyOverride,
    int? TimeLimitMinutesOverride,
    IReadOnlyList<HintRequest>? Hints);

/// <summary>
/// A piece of help attached to a Search. It may either accompany the search or give the solution away,
/// and may optionally carry coordinates.
/// </summary>
/// <param name="Id" example="8c1d3f56-7a24-4e19-9db8-0f5a2c6b3e71">Null asks the server to mint a new id. Reuse a known id to keep this Hint's identity across an update.</param>
/// <param name="Order" example="1">Position among the sibling Hints; must be unique within the Search. Drives the order help is offered in.</param>
/// <param name="Content" example="It faces the cathedral's north door.">The help text shown to participants.</param>
/// <param name="IsSolution">Marks this Hint as revealing the answer outright rather than nudging toward it.</param>
/// <param name="Latitude" example="-34.603722">Must be sent together with <paramref name="Longitude"/> or not at all — one without the other is rejected. Null means the Hint carries no location. Range -90 to 90.</param>
/// <param name="Longitude" example="-58.381592">Must be sent together with <paramref name="Latitude"/> or not at all. Null means the Hint carries no location. Range -180 to 180.</param>
public sealed record HintRequest(
    Guid? Id,
    int Order,
    string Content,
    bool IsSolution,
    double? Latitude,
    double? Longitude);
