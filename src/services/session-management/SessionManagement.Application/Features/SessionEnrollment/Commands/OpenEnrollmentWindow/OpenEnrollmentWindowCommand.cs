using MediatR;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed record OpenEnrollmentWindowCommand(Guid LiveSessionId) : IRequest<EnrollmentWindowResponse>;

