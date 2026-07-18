using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SessionManagement.Application;
using SessionManagement.Application.Features.EvidenceSubmissions;
using SessionManagement.Application.Features.Hints;
using SessionManagement.Application.Features.LiveSessions;
using SessionManagement.Application.Features.Penalties;
using SessionManagement.Application.Features.SessionEnrollment;
using SessionManagement.Application.Features.SessionLifecycle;
using Xunit;

namespace SessionManagement.UnitTests.Features;

public sealed class SessionCommandValidationTests
{
    [Fact]
    public void ApplicationRegistration_RegistersEverySessionCommandValidator()
    {
        var services = new ServiceCollection();
        services.AddSessionManagementApplication();
        using var provider = services.BuildServiceProvider();

        Assert.NotEmpty(provider.GetServices<IValidator<GenerateJoinCodeCommand>>());
        Assert.NotEmpty(provider.GetServices<IValidator<OpenEnrollmentWindowCommand>>());
        Assert.NotEmpty(provider.GetServices<IValidator<CloseEnrollmentWindowCommand>>());
        Assert.NotEmpty(provider.GetServices<IValidator<JoinSessionTeamCommand>>());
        Assert.NotEmpty(provider.GetServices<IValidator<RegisterTeamCommand>>());
        Assert.NotEmpty(provider.GetServices<IValidator<RegisterTeamByOperatorCommand>>());
        Assert.NotEmpty(provider.GetServices<IValidator<CreateLiveSessionCommand>>());
        Assert.NotEmpty(provider.GetServices<IValidator<DeactivateStageCommand>>());
        Assert.NotEmpty(provider.GetServices<IValidator<TransitionLiveSessionStateCommand>>());
        Assert.NotEmpty(provider.GetServices<IValidator<CreateOperationalHintCommand>>());
        Assert.NotEmpty(provider.GetServices<IValidator<ReleaseHintCommand>>());
        Assert.NotEmpty(provider.GetServices<IValidator<SubmitEvidenceCommand>>());
        Assert.NotEmpty(provider.GetServices<IValidator<SubmitTriviaAnswerCommand>>());
        Assert.NotEmpty(provider.GetServices<IValidator<OverrideValidationOutcomeCommand>>());
        Assert.NotEmpty(provider.GetServices<IValidator<ApplyPenaltyCommand>>());
    }

    [Theory]
    [MemberData(nameof(InvalidCommands))]
    public void Validators_RejectInvalidCommandWithStableErrorCode(Func<FluentValidation.Results.ValidationResult> validate, string errorCode)
    {
        var result = validate();

        Assert.False(result.IsValid);
        Assert.Equal(errorCode, result.Errors[0].ErrorCode);
    }

    public static TheoryData<Func<FluentValidation.Results.ValidationResult>, string> InvalidCommands() => new()
    {
        { () => new GenerateJoinCodeCommandValidator().Validate(new GenerateJoinCodeCommand(Guid.Empty)), "live_session_id_required" },
        { () => new OpenEnrollmentWindowCommandValidator().Validate(new OpenEnrollmentWindowCommand(Guid.Empty)), "live_session_id_required" },
        { () => new CloseEnrollmentWindowCommandValidator().Validate(new CloseEnrollmentWindowCommand(Guid.Empty)), "live_session_id_required" },
        { () => new JoinSessionTeamCommandValidator().Validate(new JoinSessionTeamCommand("", Guid.NewGuid())), "join_code_required" },
        { () => new RegisterTeamCommandValidator().Validate(new RegisterTeamCommand("ABC234", "")), "session_team_name_required" },
        { () => new RegisterTeamByOperatorCommandValidator().Validate(new RegisterTeamByOperatorCommand(Guid.Empty, "Team")), "live_session_id_required" },
        { () => new CreateLiveSessionCommandValidator().Validate(new CreateLiveSessionCommand(Guid.NewGuid(), "Session", null, null)), "live_session_stage_flow_required" },
        { () => new DeactivateStageCommandValidator().Validate(new DeactivateStageCommand(Guid.Empty, Guid.NewGuid())), "live_session_id_required" },
        { () => new TransitionLiveSessionStateCommandValidator().Validate(new TransitionLiveSessionStateCommand(Guid.NewGuid(), (LiveSessionLifecycleAction)99)), "live_session_lifecycle_action_invalid" },
        { () => new CreateOperationalHintCommandValidator().Validate(new CreateOperationalHintCommand(Guid.NewGuid(), Guid.NewGuid(), "Hint", 1, null)), "live_session_stage_hint_coordinates_incomplete" },
        { () => new ReleaseHintCommandValidator().Validate(new ReleaseHintCommand(Guid.Empty, null, Guid.NewGuid())), "live_session_id_required" },
        { () => new SubmitEvidenceCommandValidator().Validate(new SubmitEvidenceCommand(Guid.Empty, "hash")), "session_team_id_required" },
        { () => new SubmitTriviaAnswerCommandValidator().Validate(new SubmitTriviaAnswerCommand(Guid.NewGuid(), Guid.Empty)), "selected_choice_id_required" },
        { () => new OverrideValidationOutcomeCommandValidator().Validate(new OverrideValidationOutcomeCommand(Guid.Empty, true, "Reason")), "evidence_submission_id_required" },
        { () => new ApplyPenaltyCommandValidator().Validate(new ApplyPenaltyCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "Minor", "Reason")), "penalty_command_id_required" }
    };
}
