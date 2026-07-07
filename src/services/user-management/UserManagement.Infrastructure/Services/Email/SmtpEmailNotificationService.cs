using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using UserManagement.Application.Abstractions;

namespace UserManagement.Infrastructure.Services.Email;

public sealed class SmtpEmailNotificationService(
    IOptions<SmtpEmailOptions> options,
    ILogger<SmtpEmailNotificationService> logger) : IEmailNotificationService
{
    private readonly SmtpEmailOptions _options = options.Value;

    public async Task SendOperatorCredentialsAsync(
        string email, string username, string rawPassword, CancellationToken cancellationToken = default)
    {
        var subject = "Bienvenido a UMBRAL — Tus credenciales de acceso";

        var htmlBody = BuildHtmlEmail(
            greeting: $"Hola, {username}",
            headline: "Tu cuenta de operador ha sido creada",
            bodyParagraph: "Un administrador te ha dado acceso a la plataforma <strong style=\"color:#e8a84c;\">UMBRAL</strong>. A continuación encontrarás tus credenciales para iniciar sesión:",
            credentials: new[] { ("Usuario", username), ("Contraseña", rawPassword) },
            footerNote: "Te recomendamos cambiar tu contraseña después del primer inicio de sesión.");

        await SendAsync(email, subject, htmlBody, cancellationToken);
    }

    public async Task SendPasswordRotatedAsync(
        string email, string username, string newPassword, CancellationToken cancellationToken = default)
    {
        var subject = "UMBRAL — Tu contraseña ha sido actualizada";

        var htmlBody = BuildHtmlEmail(
            greeting: $"Hola, {username}",
            headline: "Tu contraseña ha sido actualizada",
            bodyParagraph: "Un administrador ha actualizado la contraseña de tu cuenta en la plataforma <strong style=\"color:#e8a84c;\">UMBRAL</strong>. A continuación encontrarás tus nuevas credenciales:",
            credentials: new[] { ("Usuario", username), ("Nueva Contraseña", newPassword) },
            footerNote: "Si no solicitaste este cambio, contacta a tu administrador de inmediato.");

        await SendAsync(email, subject, htmlBody, cancellationToken);
    }

    private async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();

        // SecureSocketOptions.Auto: allows both plain (Mailpit) and SSL/TLS (Gmail) based on port
        var socketOptions = _options.UseSsl
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTlsWhenAvailable;

        await client.ConnectAsync(_options.Host, _options.Port, socketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        logger.LogInformation("Email sent to {Recipient} with subject \"{Subject}\".", to, subject);
    }

    // ponytail: single method builds all email variants — no template engine needed yet.
    private static string BuildHtmlEmail(
        string greeting,
        string headline,
        string bodyParagraph,
        (string Label, string Value)[] credentials,
        string footerNote)
    {
        // UMBRAL Design System colors (from index.css design tokens)
        // --bg-deep: #08080a | --bg: #0f0f12 | --surface: #1c1c20
        // --accent: #c4841d | --accent-text: #e8a84c | --text: #e8e6e1
        // --text-secondary: #9d9a94 | --text-muted: #5c5a55
        // --border: rgba(255,255,255,0.06) | --border-strong: rgba(255,255,255,0.12)

        var credentialRows = string.Join("\n", credentials.Select(c =>
            $"""
                            <tr>
                                <td style="padding:10px 16px;font-weight:600;color:#9d9a94;text-transform:uppercase;font-size:11px;letter-spacing:1.5px;border-bottom:1px solid rgba(255,255,255,0.06);width:150px;font-family:'IBM Plex Sans',system-ui,sans-serif;">{c.Label}</td>
                                <td style="padding:10px 16px;font-family:'IBM Plex Mono','Fira Code','Courier New',monospace;font-size:14px;color:#e8e6e1;border-bottom:1px solid rgba(255,255,255,0.06);letter-spacing:0.5px;">{c.Value}</td>
                            </tr>
            """));

        return $$"""
        <!DOCTYPE html>
        <html lang="es">
        <head>
            <meta charset="UTF-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1.0" />
            <title>{{headline}}</title>
        </head>
        <body style="margin:0;padding:0;background-color:#08080a;font-family:'IBM Plex Sans',system-ui,-apple-system,sans-serif;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#08080a;padding:40px 0;">
                <tr>
                    <td align="center">
                        <table role="presentation" width="560" cellpadding="0" cellspacing="0" style="background-color:#0f0f12;border-radius:16px;border:1px solid rgba(255,255,255,0.06);overflow:hidden;box-shadow:0 12px 40px rgba(0,0,0,0.6),0 0 24px rgba(196,132,29,0.08);">

                            <!-- Top accent bar — amber gold -->
                            <tr>
                                <td style="height:3px;background:linear-gradient(90deg,#c4841d,#e8a84c,#c4841d);"></td>
                            </tr>

                            <!-- Brand header -->
                            <tr>
                                <td style="padding:32px 40px 20px 40px;text-align:center;background-color:#0f0f12;">
                                    <span style="font-size:26px;font-weight:700;letter-spacing:8px;color:#e8a84c;text-transform:uppercase;font-family:'IBM Plex Sans',system-ui,sans-serif;">UMBRAL</span>
                                    <div style="margin-top:5px;font-size:10px;color:#5c5a55;letter-spacing:4px;text-transform:uppercase;font-family:'IBM Plex Sans',system-ui,sans-serif;">Plataforma de Simulación</div>
                                </td>
                            </tr>

                            <!-- Divider -->
                            <tr>
                                <td style="padding:0 40px;">
                                    <div style="height:1px;background:rgba(255,255,255,0.06);"></div>
                                </td>
                            </tr>

                            <!-- Body content area -->
                            <tr>
                                <td style="padding:28px 40px 0 40px;background-color:#0f0f12;">

                                    <!-- Section tag -->
                                    <div style="margin-bottom:12px;">
                                        <span style="font-size:10px;font-weight:600;color:#c4841d;text-transform:uppercase;letter-spacing:2.5px;font-family:'IBM Plex Sans',system-ui,sans-serif;">Acceso de Operador</span>
                                    </div>

                                    <!-- Greeting -->
                                    <p style="margin:0 0 6px 0;font-size:14px;color:#9d9a94;font-family:'IBM Plex Sans',system-ui,sans-serif;">{{greeting}},</p>

                                    <!-- Headline -->
                                    <h1 style="margin:0 0 16px 0;font-size:21px;font-weight:600;color:#e8e6e1;line-height:1.3;font-family:'IBM Plex Sans',system-ui,sans-serif;">{{headline}}</h1>

                                    <!-- Body paragraph -->
                                    <p style="margin:0 0 24px 0;font-size:14px;line-height:1.7;color:#9d9a94;font-family:'IBM Plex Sans',system-ui,sans-serif;">{{bodyParagraph}}</p>
                                </td>
                            </tr>

                            <!-- Credentials card -->
                            <tr>
                                <td style="padding:0 40px 24px 40px;background-color:#0f0f12;">
                                    <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#1c1c20;border-radius:8px;border:1px solid rgba(255,255,255,0.06);overflow:hidden;">
                                        <!-- Card header -->
                                        <tr>
                                            <td colspan="2" style="padding:12px 16px;border-bottom:1px solid rgba(255,255,255,0.06);">
                                                <span style="font-size:10px;font-weight:700;color:#c4841d;text-transform:uppercase;letter-spacing:2.5px;font-family:'IBM Plex Sans',system-ui,sans-serif;">&#128274; Credenciales</span>
                                            </td>
                                        </tr>
        {{credentialRows}}
                                    </table>
                                </td>
                            </tr>

                            <!-- Footer note -->
                            <tr>
                                <td style="padding:0 40px 28px 40px;background-color:#0f0f12;">
                                    <table role="presentation" cellpadding="0" cellspacing="0" style="background:rgba(196,132,29,0.08);border-radius:6px;border-left:2px solid #c4841d;width:100%;">
                                        <tr>
                                            <td style="padding:12px 16px;">
                                                <p style="margin:0;font-size:13px;color:#e8a84c;line-height:1.6;font-family:'IBM Plex Sans',system-ui,sans-serif;">{{footerNote}}</p>
                                            </td>
                                        </tr>
                                    </table>
                                </td>
                            </tr>

                            <!-- Bottom divider -->
                            <tr>
                                <td style="padding:0 40px;">
                                    <div style="height:1px;background:rgba(255,255,255,0.06);"></div>
                                </td>
                            </tr>

                            <!-- Footer -->
                            <tr>
                                <td style="padding:16px 40px 24px 40px;text-align:center;background-color:#0f0f12;">
                                    <p style="margin:0;font-size:11px;color:#5c5a55;font-family:'IBM Plex Sans',system-ui,sans-serif;">Este es un correo automático de la plataforma UMBRAL.<br/>No responda a este mensaje.</p>
                                </td>
                            </tr>

                            <!-- Bottom accent bar -->
                            <tr>
                                <td style="height:2px;background:rgba(196,132,29,0.3);"></td>
                            </tr>
                        </table>

                        <!-- Sub-footer -->
                        <table role="presentation" width="560" cellpadding="0" cellspacing="0">
                            <tr>
                                <td style="padding:16px 0;text-align:center;">
                                    <p style="margin:0;font-size:10px;color:#5c5a55;font-family:'IBM Plex Sans',system-ui,sans-serif;">&copy; UMBRAL — Grupo 2</p>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>
        </body>
        </html>
        """;
    }
}
