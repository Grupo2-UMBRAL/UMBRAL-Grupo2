using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using SessionOperations.Application.Abstractions;

namespace SessionOperations.Application.Features.SessionEnrollment;

public sealed record GenerateJoinCodeCommand(Guid LiveSessionId) : IRequest<GenerateJoinCodeResponse>;

public sealed class GenerateJoinCodeHandler(
    ISessionOperationsDbContext dbContext,
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

        var liveSession = await dbContext.LiveSessions
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
            var exists = await dbContext.LiveSessions
                .AnyAsync(session => session.JoinCodeValue == joinCode.Value, cancellationToken);
            if (exists)
            {
                continue;
            }

            liveSession.AssignJoinCode(joinCode);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new GenerateJoinCodeResponse(liveSession.Id, joinCode.Value);
        }

        throw new UmbralDomainException(
            "join_code_generation_exhausted",
            "Could not generate a unique Join Code.",
            UmbralFailureCategory.Technical);
    }
}
