using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Umbral.Api.Middleware;
using Umbral.Application.Common.Behaviors;
using Umbral.Application.Common.Interfaces;
using Umbral.Domain.Missions.Repositories;
using Umbral.Domain.Scoring.Repositories;
using Umbral.Domain.Sessions.Repositories;
using Umbral.Domain.Teams.Repositories;
using Umbral.Domain.Users.Repositories;
using Umbral.Infrastructure.Persistence;
using Umbral.Infrastructure.Persistence.Repositories;
using Umbral.Infrastructure.RealTime;
using Umbral.Infrastructure.Security;
using Umbral.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// BASE DE DATOS — EF Core + PostgreSQL
builder.Services.AddDbContext<UmbralDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// MEDIATR — CQRS Pipeline
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Umbral.Application.Common.Behaviors.ValidationBehavior<,>).Assembly));

// Pipeline Behaviors: se ejecutan en orden de registro
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// FLUENTVALIDATION — Registro automático de validators
builder.Services.AddValidatorsFromAssembly(
    typeof(Umbral.Application.Common.Behaviors.ValidationBehavior<,>).Assembly);

// REPOSITORIOS — Inyección de dependencias (DIP)
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IMissionRepository, MissionRepository>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<ITeamRepository, TeamRepository>();
builder.Services.AddScoped<IEvidenceRepository, EvidenceRepository>();
builder.Services.AddScoped<IScoreEntryRepository, ScoreEntryRepository>();
builder.Services.AddScoped<IPenaltyRepository, PenaltyRepository>();

// SERVICIOS DE APLICACIÓN
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<INotificationService, SignalRNotificationService>();
builder.Services.AddScoped<IOutboxService, OutboxService>();
builder.Services.AddHttpContextAccessor();

// AUTENTICACIÓN JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!))
        };

        // Permitir que SignalR reciba el token via query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/session-hub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// SIGNALR — Tiempo real
builder.Services.AddSignalR();

// CONTROLLERS + CORS
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

var app = builder.Build();

// MIDDLEWARE PIPELINE
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<SessionHub>("/session-hub");

app.Run();
