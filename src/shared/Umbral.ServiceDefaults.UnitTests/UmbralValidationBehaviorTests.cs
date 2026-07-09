using FluentValidation;
using Umbral.ServiceDefaults;
using Xunit;

namespace Umbral.ServiceDefaults.UnitTests;

public sealed class UmbralValidationBehaviorTests
{
    private sealed record SampleRequest(string? Name, string? Email);

    private static UmbralValidationBehavior<SampleRequest, string> Behavior(params IValidator<SampleRequest>[] validators) =>
        new(validators);

    [Fact]
    public async Task Handle_NoValidators_PassesThrough()
    {
        var behavior = Behavior();

        var response = await behavior.Handle(
            new SampleRequest("x", "x@y.z"), () => Task.FromResult("ok"), CancellationToken.None);

        Assert.Equal("ok", response);
    }

    [Fact]
    public async Task Handle_ValidRequest_InvokesNext()
    {
        var validator = new InlineValidator<SampleRequest>();
        validator.RuleFor(x => x.Name).NotEmpty().WithErrorCode("name_required");
        var behavior = Behavior(validator);

        var response = await behavior.Handle(
            new SampleRequest("John", "j@y.z"), () => Task.FromResult("ok"), CancellationToken.None);

        Assert.Equal("ok", response);
    }

    [Fact]
    public async Task Handle_InvalidRequest_ThrowsValidationCategorizedException_WithRuleCode()
    {
        var validator = new InlineValidator<SampleRequest>();
        validator.RuleFor(x => x.Name).NotEmpty().WithErrorCode("name_required").WithMessage("Name is required.");
        var behavior = Behavior(validator);
        var nextCalled = false;

        var ex = await Assert.ThrowsAsync<UmbralDomainException>(() => behavior.Handle(
            new SampleRequest(null, "j@y.z"),
            () => { nextCalled = true; return Task.FromResult("ok"); },
            CancellationToken.None));

        Assert.Equal("name_required", ex.Code);
        Assert.Equal("Name is required.", ex.Message);
        Assert.Equal(UmbralFailureCategory.Validation, ex.Category);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task Handle_FailFast_SurfacesFirstErrorInDeclaredOrder()
    {
        // CascadeMode.Stop -> Errors[0] is the first failing rule; the behavior surfaces exactly that.
        var validator = new InlineValidator<SampleRequest> { ClassLevelCascadeMode = CascadeMode.Stop };
        validator.RuleFor(x => x.Name).NotEmpty().WithErrorCode("name_required");
        validator.RuleFor(x => x.Email).NotEmpty().WithErrorCode("email_required");
        var behavior = Behavior(validator);

        var ex = await Assert.ThrowsAsync<UmbralDomainException>(() => behavior.Handle(
            new SampleRequest(null, null), () => Task.FromResult("ok"), CancellationToken.None));

        Assert.Equal("name_required", ex.Code);
    }
}
