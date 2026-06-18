using MediatR;

namespace SessionManagement.Application.Features.SessionSnapshots;

public sealed record GetLiveSessionOverviewQuery(Guid LiveSessionId) : IRequest<LiveSessionOverview>;
