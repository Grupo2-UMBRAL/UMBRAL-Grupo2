using MediatR;

namespace SessionManagement.Application.Features.EvidenceSubmissions;

public sealed record SubmitTriviaAnswerCommand(Guid SessionTeamId, Guid SelectedChoiceId) : IRequest<SubmitEvidenceResponse>;

/// <summary>
/// A Session Team's attempt to solve its current Trivia Play by picking one alternative.
/// </summary>
/// <param name="SelectedChoiceId" example="4d6e2a10-7c95-4b83-a1f2-8e0d9c7b6a53">Id of the chosen alternative, taken from the current Play's choices. Free text is never accepted: the server compares this id against the correct one, which never leaves the backend.</param>
public sealed record SubmitTriviaAnswerRequest(Guid SelectedChoiceId);
