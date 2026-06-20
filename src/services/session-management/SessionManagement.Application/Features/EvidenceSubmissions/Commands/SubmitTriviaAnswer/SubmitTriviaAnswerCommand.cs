using MediatR;

namespace SessionManagement.Application.Features.EvidenceSubmissions;

public sealed record SubmitTriviaAnswerCommand(Guid SessionTeamId, string AnswerText) : IRequest<SubmitEvidenceResponse>;

public sealed record SubmitTriviaAnswerRequest(string AnswerText);

