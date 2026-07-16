namespace MissionManagement.Application.Common.Dtos;

// ---------------------------------------------------------------------------
// Eligible-for-live-session contracts (server-to-server; consumed by session-management)
// ---------------------------------------------------------------------------

/// <summary>
/// A Mission that is active and currently eligible to back a LiveSession, reduced to what a picker
/// needs. Missions that are active but not eligible are left out of the listing entirely.
/// </summary>
/// <param name="Id" example="c17a4e29-8b56-4f03-9a7d-1e6b2c8d4f50">Use it to fetch the full eligible view with the plays.</param>
/// <param name="Name" example="Downtown treasure hunt">Unique across all Missions.</param>
/// <param name="MaximumDurationMinutes" example="120">Wall-clock budget for the whole Mission run, in minutes.</param>
/// <param name="GameType">Always null. A Mission no longer has one Game Type of its own — it derives them from its Challenges, which may mix Trivia and Treasure Hunt. Read the Game Type per play instead.</param>
/// <param name="ActivePlayCount" example="12">How many plays the flatten yields — the length of the linear path a participant will actually run. Counts plays, not Challenges, and excludes anything under an inactive or malformed Challenge.</param>
public sealed record EligibleMissionForLiveSessionSummaryResponse(
    Guid Id,
    string Name,
    int MaximumDurationMinutes,
    string? GameType,
    int ActivePlayCount);

/// <summary>
/// The runnable view of an eligible Mission, consumed server-to-server by session-management. The
/// Section tree is gone: the path arrives already flattened into the linear sequence of plays.
/// </summary>
/// <param name="Id" example="c17a4e29-8b56-4f03-9a7d-1e6b2c8d4f50">The Mission this path was flattened from.</param>
/// <param name="Name" example="Downtown treasure hunt">Unique across all Missions.</param>
/// <param name="Description" example="Two-hour run around the old town mixing QR searches with trivia stops.">Operator-facing blurb used when picking a Mission for a LiveSession.</param>
/// <param name="MaximumDurationMinutes" example="120">Wall-clock budget for the whole Mission run, in minutes. Independent of the per-play Time Limits.</param>
/// <param name="Plays">The flattened linear path, depth-first through the Sections and in Order. Never empty — a Mission with no plays is not eligible. Sections and Challenges are not represented here; only their plays survive.</param>
public sealed record EligibleMissionForLiveSessionResponse(
    Guid Id,
    string Name,
    string Description,
    int MaximumDurationMinutes,
    IReadOnlyList<EligiblePlayResponse> Plays);

/// <summary>
/// One play of the flattened path, with Difficulty and Time Limit already resolved. Its shape follows
/// <paramref name="GameType"/>: the fields belonging to the other Game Type come back null or empty,
/// so null here means "not applicable to this Game Type" rather than "unset". Server-to-server only —
/// this shape carries the correct answer and must not be relayed to participants as-is.
/// </summary>
/// <param name="Id" example="2b8f6d10-4c93-4a57-8e21-b7d5c3f09a64">The id of the underlying Question or Search — the handle other contexts credit a score against.</param>
/// <param name="Order" example="1">Global 1-based position across the whole flattened path, assigned by the flatten. This is not the Order authored on the play.</param>
/// <param name="GameType" example="Trivia">"Trivia" or "Treasure Hunt", taken from the owning Challenge. Read it before trusting the nullable fields below.</param>
/// <param name="Difficulty" example="Medium">The effective Difficulty, already resolved as the play's own override or else the Challenge default. Never null and never needs resolving downstream.</param>
/// <param name="TimeLimitMinutes" example="5">The effective Time Limit for this play in minutes, already resolved as the play's own override or else the Challenge default.</param>
/// <param name="Prompt" example="Which building is the oldest in the square?">The player-facing text, whichever the Game Type calls for: a Question's text or a Search's clue.</param>
/// <param name="Choices">The answer options for a Trivia play, in order, stripped of the correct flag. Null for a Treasure Hunt play.</param>
/// <param name="CorrectChoiceId" example="6d4a2c88-1f37-4b90-a5e6-3c8b7d2e1904">Which of the Choices is correct, for a Trivia play. Null for a Treasure Hunt play, whose answer is the QR hash instead. Never forward this to a participant.</param>
/// <param name="ExpectedQrHash" example="4f2a9c1d8b3e6057">The hash a scanned QR must match, for a Treasure Hunt play. Null for a Trivia play. Never forward this to a participant.</param>
/// <param name="Hints">The Hints of a Treasure Hunt play, in order. Empty for a Trivia play, which never has Hints, and also empty for a Search authored without any.</param>
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
/// <summary>
/// An answer option as a participant may see it. Deliberately omits the correct flag that the
/// authoring view carries — correctness travels only as the sibling CorrectChoiceId.
/// </summary>
/// <param name="Id" example="6d4a2c88-1f37-4b90-a5e6-3c8b7d2e1904">What an answer submission refers back to, and what CorrectChoiceId is compared against.</param>
/// <param name="Text" example="The clock tower">The option shown to participants.</param>
public sealed record EligibleChoiceResponse(Guid Id, string Text);

/// <summary>
/// A Hint of a Treasure Hunt play as a participant may see it. Carries no id: the Hints of a play are
/// identified by their position in the list.
/// </summary>
/// <param name="Content" example="It faces the cathedral's north door.">The help text shown to participants.</param>
/// <param name="IsSolution">Whether this Hint gives the answer away rather than nudging toward it. Gate the reveal on this rather than showing every Hint at once.</param>
/// <param name="Latitude" example="-34.603722">Null — always together with <paramref name="Longitude"/> — means the Hint carries no location and cannot be mapped.</param>
/// <param name="Longitude" example="-58.381592">Null — always together with <paramref name="Latitude"/> — means the Hint carries no location.</param>
public sealed record EligibleHintResponse(string Content, bool IsSolution, double? Latitude, double? Longitude);
