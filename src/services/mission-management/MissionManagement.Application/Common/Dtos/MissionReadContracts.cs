namespace MissionManagement.Application.Common.Dtos;

// ---------------------------------------------------------------------------
// Read contracts (admin authors and sees correct answers — ADR §9)
// ---------------------------------------------------------------------------

/// <summary>
/// The full authoring view of a Mission, returned to administrators. Unlike the eligible views served
/// to a LiveSession, this one deliberately exposes the correct answers.
/// </summary>
/// <param name="Id" example="c17a4e29-8b56-4f03-9a7d-1e6b2c8d4f50">Server-assigned; stable across updates and the handle used to activate, update or reuse the Mission.</param>
/// <param name="Name" example="Downtown treasure hunt">Unique across all Missions.</param>
/// <param name="Description" example="Two-hour run around the old town mixing QR searches with trivia stops.">Operator-facing blurb used when picking a Mission for a LiveSession.</param>
/// <param name="MaximumDurationMinutes" example="120">Wall-clock budget for the whole Mission run, in minutes.</param>
/// <param name="IsActive">Whether the Mission has been activated. Only an active Mission can back a LiveSession, and while active its path is held to the eligibility rules on every update.</param>
/// <param name="Items">The root Path Items in path order, Sections still nested rather than flattened. Empty when the Mission has no path yet.</param>
public sealed record MissionResponse(
    Guid Id,
    string Name,
    string Description,
    int MaximumDurationMinutes,
    bool IsActive,
    IReadOnlyList<MissionItemResponse> Items);

/// <summary>
/// A Mission reduced to what a listing needs. The path is omitted entirely — fetch the Mission by id
/// to see its Path Items.
/// </summary>
/// <param name="Id" example="c17a4e29-8b56-4f03-9a7d-1e6b2c8d4f50">Server-assigned; use it to fetch the full Mission.</param>
/// <param name="Name" example="Downtown treasure hunt">Unique across all Missions.</param>
/// <param name="MaximumDurationMinutes" example="120">Wall-clock budget for the whole Mission run, in minutes.</param>
/// <param name="IsActive">Whether the Mission has been activated. Active does not by itself promise the Mission is currently eligible for a LiveSession.</param>
public sealed record MissionSummaryResponse(
    Guid Id,
    string Name,
    int MaximumDurationMinutes,
    bool IsActive);

/// <summary>
/// A Path Item in the authoring view, in either of its two shapes. Which fields are populated follows
/// from <paramref name="Kind"/>: the fields of the other shape come back null, so null here means
/// "not applicable to this shape" rather than "unset".
/// </summary>
/// <param name="Kind" example="Challenge">Discriminates the shape of this item: "Section" or "Challenge". Read it before trusting any of the nullable fields below.</param>
/// <param name="Id" example="9f3c1b7e-5a41-4d2e-9b28-6c0d4a1f8e73">Send it back on update to keep this item's identity, since an update replaces the path wholesale.</param>
/// <param name="Order" example="1">Position among siblings only — not a global index into the path.</param>
/// <param name="Title" example="Old town square">Populated for both shapes in practice — titles are required at authoring time — and nullable only because Sections and Challenges share this shape. On a Section this is its only real content.</param>
/// <param name="Children">Nested Path Items in order for a Section, Sections included. Null for a Challenge, which never nests.</param>
/// <param name="GameType" example="Treasure Hunt">"Trivia" or "Treasure Hunt" for a Challenge. Null for a Section, which is inert and has no Game Type of its own.</param>
/// <param name="Difficulty" example="Medium">The Challenge default Difficulty that plays inherit unless they override it. Null for a Section.</param>
/// <param name="TimeLimitMinutes" example="5">The Challenge default Time Limit per play, in minutes, inherited unless a play overrides it. Null for a Section.</param>
/// <param name="IsActive">Whether a Challenge's plays reach a LiveSession; an inactive Challenge stays in the path but is skipped by the flatten. Null for a Section, which is never active or inactive.</param>
/// <param name="Questions">The Trivia plays of a Challenge, with their correct Choices exposed. Null for a Section, and empty for a Challenge that has no Questions.</param>
/// <param name="Searches">The Treasure Hunt plays of a Challenge. Null for a Section, and empty for a Challenge that has no Searches.</param>
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

/// <summary>
/// A Trivia play in the authoring view. The overrides are echoed back raw, not resolved — to see the
/// effective values a LiveSession would use, read the eligible view instead.
/// </summary>
/// <param name="Id" example="2b8f6d10-4c93-4a57-8e21-b7d5c3f09a64">Send it back on update to keep this Question's identity.</param>
/// <param name="Order" example="1">Position among the plays of the owning Challenge only.</param>
/// <param name="Text" example="Which building is the oldest in the square?">The prompt shown to participants.</param>
/// <param name="DifficultyOverride" example="Hard">The Difficulty authored on this Question alone. Null means it inherits the Challenge default, not that it has no Difficulty.</param>
/// <param name="TimeLimitMinutesOverride" example="2">The Time Limit authored on this Question alone. Null means it inherits the Challenge default.</param>
/// <param name="Choices">The 2 to 4 Choices in Order, exactly one of them flagged correct.</param>
public sealed record QuestionResponse(
    Guid Id,
    int Order,
    string Text,
    string? DifficultyOverride,
    int? TimeLimitMinutesOverride,
    IReadOnlyList<ChoiceResponse> Choices);

/// <summary>
/// One answer option of a Question, in the authoring view. This shape carries the correct-answer flag
/// and so is only ever served to administrators.
/// </summary>
/// <param name="Id" example="6d4a2c88-1f37-4b90-a5e6-3c8b7d2e1904">Send it back on update to keep this Choice's identity.</param>
/// <param name="Order" example="1">Display position among the sibling Choices; unrelated to correctness.</param>
/// <param name="Text" example="The clock tower">The option shown to participants.</param>
/// <param name="IsCorrect">Set on exactly one Choice per Question. Exposed here by design; the player-facing view of a Choice omits it.</param>
public sealed record ChoiceResponse(
    Guid Id,
    int Order,
    string Text,
    bool IsCorrect);

/// <summary>
/// A Treasure Hunt play in the authoring view, including the QR hash that resolves it.
/// </summary>
/// <param name="Id" example="0a7e5b32-9d16-4c48-bf03-2e91a6d47c85">Send it back on update to keep this Search's identity.</param>
/// <param name="Order" example="1">Position among the plays of the owning Challenge only.</param>
/// <param name="Clue" example="Look where the water has run since 1890.">The prompt that leads participants toward the QR code.</param>
/// <param name="ExpectedQrHash" example="4f2a9c1d8b3e6057">The hash a scanned QR must match to resolve this Search.</param>
/// <param name="DifficultyOverride" example="Easy">The Difficulty authored on this Search alone. Null means it inherits the Challenge default.</param>
/// <param name="TimeLimitMinutesOverride" example="10">The Time Limit authored on this Search alone. Null means it inherits the Challenge default.</param>
/// <param name="Hints">The Hints of this Search in Order. Empty when none were authored — Hints are optional.</param>
public sealed record SearchResponse(
    Guid Id,
    int Order,
    string Clue,
    string ExpectedQrHash,
    string? DifficultyOverride,
    int? TimeLimitMinutesOverride,
    IReadOnlyList<HintResponse> Hints);

/// <summary>
/// A Hint of a Search in the authoring view.
/// </summary>
/// <param name="Id" example="8c1d3f56-7a24-4e19-9db8-0f5a2c6b3e71">Send it back on update to keep this Hint's identity.</param>
/// <param name="Order" example="1">Position among the sibling Hints; drives the order help is offered in.</param>
/// <param name="Content" example="It faces the cathedral's north door.">The help text shown to participants.</param>
/// <param name="IsSolution">Whether this Hint reveals the answer outright rather than nudging toward it.</param>
/// <param name="Latitude" example="-34.603722">Null — always together with <paramref name="Longitude"/> — means the Hint carries no location; the pair is stored all-or-nothing.</param>
/// <param name="Longitude" example="-58.381592">Null — always together with <paramref name="Latitude"/> — means the Hint carries no location.</param>
public sealed record HintResponse(
    Guid Id,
    int Order,
    string Content,
    bool IsSolution,
    double? Latitude,
    double? Longitude);
