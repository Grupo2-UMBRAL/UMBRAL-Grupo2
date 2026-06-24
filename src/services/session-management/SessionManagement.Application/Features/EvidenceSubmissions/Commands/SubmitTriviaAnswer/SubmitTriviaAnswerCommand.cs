using MediatR;

namespace SessionManagement.Application.Features.EvidenceSubmissions;

public sealed record SubmitTriviaAnswerCommand(Guid SessionTeamId, Guid SelectedChoiceId) : IRequest<SubmitEvidenceResponse>;

public sealed record SubmitTriviaAnswerRequest(Guid SelectedChoiceId);
