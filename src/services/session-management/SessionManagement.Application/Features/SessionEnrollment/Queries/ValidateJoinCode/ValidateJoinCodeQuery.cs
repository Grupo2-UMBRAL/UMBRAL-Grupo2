using MediatR;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed record ValidateJoinCodeQuery(string JoinCode) : IRequest<ParticipantEnrollmentStatusResponse>;

