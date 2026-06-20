using MediatR;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed record CloseEnrollmentWindowCommand(Guid LiveSessionId) : IRequest<EnrollmentWindowResponse>;

