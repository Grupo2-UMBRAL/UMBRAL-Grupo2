using SessionManagement.Domain.LiveSessions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using SessionManagement.Application.Abstractions;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed class GenerateJoinCodeHandler(
    IUnitOfWork unitOfWork, IRepository<LiveSession> liveSessionRepository,
    IJoinCodeGenerator joinCodeGenerator)
    : IRequestHandler<GenerateJoinCodeCommand, GenerateJoinCodeResponse>
{
    private const int MaximumGenerationAttempts = 10;

    public async Task<GenerateJoinCodeResponse> Handle(
        GenerateJoinCodeCommand request,
        CancellationToken cancellationToken)
    {
        if (request.LiveSessionId == Guid.Empty)
        {
            throw new UmbralDomainException(
                "live_session_id_required",
                "LiveSession id is required.",
                UmbralFailureCategory.Validation);
        }

        var liveSession = await liveSessionRepository
            .SingleOrDefaultAsync(session => session.Id == request.LiveSessionId, cancellationToken);
        if (liveSession is null)
        {
            throw new UmbralDomainException(
                "live_session_not_found",
                "LiveSession was not found.",
                UmbralFailureCategory.NotFound);
        }

        if (liveSession.JoinCodeValue is not null)
        {
            return new GenerateJoinCodeResponse(liveSession.Id, liveSession.JoinCodeValue);
        }

        for (var attempt = 0; attempt < MaximumGenerationAttempts; attempt++)
        {
            var joinCode = joinCodeGenerator.Generate();
            var exists = await liveSessionRepository
                .AnyAsync(session => session.JoinCodeValue == joinCode.Value, cancellationToken);
            if (exists)
            {
                continue;
            }

            liveSession.AssignJoinCode(joinCode);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new GenerateJoinCodeResponse(liveSession.Id, joinCode.Value);
        }

        throw new UmbralDomainException(
            "join_code_generation_exhausted",
            "Could not generate a unique Join Code.",
            UmbralFailureCategory.Technical);
    }
}



