using System;
using Xunit;
using UserManagement.Infrastructure.Services.Email;
using UserManagement.Application.Abstractions;

namespace UserManagement.UnitTests.Infrastructure;

public class SmtpEmailNotificationServiceTests
{
    [Fact]
    public void BuildHtmlEmail_FormatsCredentialsCorrectly()
    {
        var html = SmtpEmailNotificationService.BuildHtmlEmail(
            "Hola, user1",
            "Test Headline",
            "This is a test body.",
            new[] { ("Usuario", "user1"), ("Contraseña", "pass123") },
            "Test footer."
        );

        Assert.Contains("Test Headline", html);
        Assert.Contains("Hola, user1", html);
        Assert.Contains("This is a test body.", html);
        Assert.Contains("Usuario", html);
        Assert.Contains("user1", html);
        Assert.Contains("Contraseña", html);
        Assert.Contains("pass123", html);
        Assert.Contains("Test footer.", html);
    }

    [Fact]
    public void BuildHtmlEmail_EmptyCredentials_DoesNotIncludeCredentialsBlock()
    {
        var html = SmtpEmailNotificationService.BuildHtmlEmail(
            "Hola, user2",
            "Test Headline 2",
            "Body 2",
            Array.Empty<(string, string)>(),
            "Footer 2"
        );

        Assert.Contains("Test Headline 2", html);
        Assert.DoesNotContain("Credenciales", html);
    }
}
