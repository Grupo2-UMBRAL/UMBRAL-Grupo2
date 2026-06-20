using MediatR;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed record GenerateJoinCodeCommand(Guid LiveSessionId) : IRequest<GenerateJoinCodeResponse>;

