using Microsoft.AspNetCore.Authentication.JwtBearer;
using MediatR;
using Umbral.ScoringAudit.Api.Application.Audit;
using Umbral.ScoringAudit.Api.Application.Bootstrap.Commands;
using Umbral.ScoringAudit.Api.Application.Bootstrap.Queries;
using Umbral.ScoringAudit.Api.Application.Rankings;
using Umbral.ScoringAudit.Api.Application.Scoreboards;
using Umbral.ScoringAudit.Api.Hubs;
using Umbral.ScoringAudit.Api.Infrastructure;
using Umbral.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
var serviceIdentity = new ServiceIdentity("scoring-audit-service", "Scoring and Audit", "scoring-audit");

builder.Services.AddUmbralApiDefaults(
    builder.Configuration,
    options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrWhiteSpace(accessToken) && path.StartsWithSegments("/hub/scoring"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddSignalR();
builder.Services.AddMediatR(typeof(Program).Assembly);
builder.Services.AddScoringAuditInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapUmbralServiceDefaults(serviceIdentity);
var authorizedApi = app.MapUmbralAuthorizedApi(serviceIdentity);
authorizedApi
    .MapGet(
        "/bootstrap",
        async (ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetScoringAuditBootstrapDetailsQuery(), cancellationToken)));
authorizedApi.MapPost(
    "/sessions/{liveSessionId:guid}/scores",
    async (
        Guid liveSessionId,
        RecordStageCreditRequest request,
        ISender sender,
        CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(
            new RecordStageCreditCommand(
                liveSessionId,
                request.SessionTeamId,
                request.MissionStageId,
                request.Difficulty,
                request.ResolutionTime,
                request.RecordedAt,
                request.ValidationOverride),
            cancellationToken)));
authorizedApi.MapPost(
    "/sessions/{liveSessionId:guid}/penalties",
    async (
        Guid liveSessionId,
        ApplyPenaltyRequest request,
        ISender sender,
        CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(
            new ApplyPenaltyCommand(
                liveSessionId,
                request.SessionTeamId,
                request.CommandId,
                request.Severity,
                request.AppliedByOperatorUserId,
                request.Reason,
                request.RecordedAt),
            cancellationToken)));
authorizedApi.MapGet(
    "/sessions/{liveSessionId:guid}/ranking",
    async (Guid liveSessionId, ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetRankingQuery(liveSessionId), cancellationToken)));
authorizedApi.MapGet(
    "/sessions/{liveSessionId:guid}/event-log",
    async (Guid liveSessionId, ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetSessionEventLogQuery(liveSessionId), cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(UmbralRoles.Administrator, UmbralRoles.Operator));
authorizedApi.MapPost(
    "/sessions/{liveSessionId:guid}/event-log",
    async (
        Guid liveSessionId,
        LogSessionEventRequest request,
        ISender sender,
        CancellationToken cancellationToken) =>
    {
        var payload = await sender.Send(
            new LogSessionEventCommand(liveSessionId, request.EventType, request.Description),
            cancellationToken);

        return Results.Created(
            $"/api/scoring-audit/sessions/{liveSessionId}/event-log/{payload.Id}",
            payload);
    })
    .RequireAuthorization(policy => policy.RequireRole(UmbralRoles.Administrator, UmbralRoles.Operator));
authorizedApi.MapUmbralRoleSmokeRoutes(serviceIdentity);

if (builder.Configuration.GetValue("Persistence:ApplyMigrationsOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var sender = scope.ServiceProvider.GetRequiredService<ISender>();
    await sender.Send(new ApplyScoringAuditPersistenceMigrationsCommand());
}

app.MapHub<ScoringAuditHub>("/hub/scoring");

app.Run();

public partial class Program
{
}
