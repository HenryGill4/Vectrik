using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Vectrik.Services;

public class SmtpEmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailSettings> settings, ILogger<SmtpEmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendDemoRequestAsync(DemoRequestEmail request)
    {
        // 1. Send notification to the Vectrik team
        var subject = $"Demo Request: {request.Company} — {request.FullName}";
        var body = BuildNotificationHtml(request);
        await SendEmailAsync(_settings.NotificationAddress, subject, body);

        // 2. Send confirmation to the requester
        var confirmSubject = "Thanks for your interest in Vectrik";
        var confirmBody = BuildConfirmationHtml(request);
        await SendEmailAsync(request.Email, confirmSubject, confirmBody);
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody)
    {
        try
        {
            using var message = new MailMessage();
            message.From = new MailAddress(_settings.FromAddress, _settings.FromName);
            message.To.Add(new MailAddress(to));
            message.Subject = subject;
            message.Body = htmlBody;
            message.IsBodyHtml = true;

            using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort);
            client.EnableSsl = _settings.EnableSsl;
            client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.Timeout = 15000; // 15 second timeout

            await client.SendMailAsync(message);
            _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}: {Subject}", to, subject);
            // Don't rethrow — email failures should not crash the application
        }
    }

    private static string BuildNotificationHtml(DemoRequestEmail r)
    {
        var details = new List<string>
        {
            Row("Name", r.FullName),
            Row("Email", $"<a href=\"mailto:{r.Email}\">{r.Email}</a>"),
            Row("Company", r.Company),
        };
        if (!string.IsNullOrWhiteSpace(r.Phone)) details.Add(Row("Phone", r.Phone));
        if (!string.IsNullOrWhiteSpace(r.MachineCount)) details.Add(Row("Machines", r.MachineCount));
        if (!string.IsNullOrWhiteSpace(r.PrimaryProcess)) details.Add(Row("Process", r.PrimaryProcess));
        if (!string.IsNullOrWhiteSpace(r.Message)) details.Add(Row("Message", r.Message));

        return $"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"></head>
        <body style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #0f172a; color: #e2e8f0; margin: 0; padding: 32px;">
            <div style="max-width: 600px; margin: 0 auto; background: #1e293b; border-radius: 12px; overflow: hidden;">
                <div style="background: linear-gradient(135deg, #3B82F6, #06B6D4); padding: 24px 32px;">
                    <h1 style="margin: 0; color: #fff; font-size: 20px;">New Demo Request</h1>
                    <p style="margin: 4px 0 0; color: rgba(255,255,255,0.8); font-size: 14px;">{DateTime.UtcNow:MMMM d, yyyy 'at' h:mm tt} UTC</p>
                </div>
                <div style="padding: 24px 32px;">
                    <table style="width: 100%; border-collapse: collapse;">
                        {string.Join("\n", details)}
                    </table>
                </div>
                <div style="padding: 16px 32px; border-top: 1px solid #334155; color: #64748b; font-size: 12px;">
                    Sent from vectrik.com demo request form
                </div>
            </div>
        </body>
        </html>
        """;
    }

    private static string BuildConfirmationHtml(DemoRequestEmail r)
    {
        return $"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"></head>
        <body style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #0f172a; color: #e2e8f0; margin: 0; padding: 32px;">
            <div style="max-width: 600px; margin: 0 auto; background: #1e293b; border-radius: 12px; overflow: hidden;">
                <div style="background: linear-gradient(135deg, #3B82F6, #06B6D4); padding: 24px 32px; text-align: center;">
                    <h1 style="margin: 0; color: #fff; font-size: 22px;">Thanks, {r.FullName}!</h1>
                </div>
                <div style="padding: 32px; line-height: 1.6;">
                    <p style="margin: 0 0 16px;">We received your demo request for <strong>{r.Company}</strong> and we're excited to show you what Vectrik can do for your manufacturing operation.</p>
                    <p style="margin: 0 0 16px;">A member of our team will reach out within <strong>24 hours</strong> to schedule your personalized demo.</p>
                    <p style="margin: 0 0 24px;">In the meantime, here's what you can expect:</p>
                    <ul style="margin: 0 0 24px; padding-left: 20px; color: #94a3b8;">
                        <li style="margin-bottom: 8px;">A walkthrough of the production scheduler and Gantt view</li>
                        <li style="margin-bottom: 8px;">Shop floor operator experience tailored to your processes</li>
                        <li style="margin-bottom: 8px;">Quality management and traceability features</li>
                        <li style="margin-bottom: 8px;">Custom configuration for your specific workflow</li>
                    </ul>
                    <p style="margin: 0; color: #94a3b8;">Questions? Reply to this email or reach us at <a href="mailto:henry@vectrik.com" style="color: #3B82F6;">henry@vectrik.com</a></p>
                </div>
                <div style="padding: 16px 32px; border-top: 1px solid #334155; color: #64748b; font-size: 12px; text-align: center;">
                    Vectrik — Manufacturing Intelligence
                </div>
            </div>
        </body>
        </html>
        """;
    }

    public async Task SendInvitationAsync(string toEmail, string fullName, string username,
        string temporaryPassword, string loginUrl, string companyName)
    {
        var subject = $"Welcome to {companyName} on Vectrik";
        var body = BuildInvitationHtml(fullName, username, temporaryPassword, loginUrl, companyName);
        await SendEmailAsync(toEmail, subject, body);
    }

    private static string BuildInvitationHtml(string fullName, string username,
        string temporaryPassword, string loginUrl, string companyName)
    {
        return $"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"></head>
        <body style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #0f172a; color: #e2e8f0; margin: 0; padding: 32px;">
            <div style="max-width: 600px; margin: 0 auto; background: #1e293b; border-radius: 12px; overflow: hidden;">
                <div style="background: linear-gradient(135deg, #3B82F6, #06B6D4); padding: 24px 32px; text-align: center;">
                    <h1 style="margin: 0; color: #fff; font-size: 22px;">Welcome, {fullName}!</h1>
                    <p style="margin: 4px 0 0; color: rgba(255,255,255,0.8); font-size: 14px;">Your account on {companyName} is ready</p>
                </div>
                <div style="padding: 32px; line-height: 1.6;">
                    <p style="margin: 0 0 16px;">An account has been created for you on the <strong>{companyName}</strong> Vectrik manufacturing platform.</p>
                    <div style="background: #0f172a; border-radius: 8px; padding: 16px 20px; margin: 0 0 20px;">
                        <table style="width: 100%; border-collapse: collapse;">
                            {Row("Username", $"<strong>{username}</strong>")}
                            {Row("Password", $"<code style=\"background: #334155; padding: 2px 8px; border-radius: 4px; font-family: monospace;\">{temporaryPassword}</code>")}
                        </table>
                    </div>
                    <p style="margin: 0 0 24px; color: #94a3b8; font-size: 0.85rem;">You will be asked to set a new password when you first log in.</p>
                    <div style="text-align: center; margin: 0 0 24px;">
                        <a href="{loginUrl}" style="display: inline-block; background: linear-gradient(135deg, #3B82F6, #06B6D4); color: #fff; text-decoration: none; padding: 14px 32px; border-radius: 8px; font-weight: 600; font-size: 1rem;">
                            Sign In to Vectrik
                        </a>
                    </div>
                    <p style="margin: 0; color: #64748b; font-size: 0.8rem; text-align: center;">If the button doesn't work, copy this link:<br/><a href="{loginUrl}" style="color: #3B82F6; word-break: break-all;">{loginUrl}</a></p>
                </div>
                <div style="padding: 16px 32px; border-top: 1px solid #334155; color: #64748b; font-size: 12px; text-align: center;">
                    {companyName} — Powered by Vectrik
                </div>
            </div>
        </body>
        </html>
        """;
    }

    private static string Row(string label, string value) =>
        $"""<tr><td style="padding: 8px 0; color: #94a3b8; font-size: 13px; width: 100px; vertical-align: top;">{label}</td><td style="padding: 8px 0; color: #e2e8f0; font-size: 14px;">{value}</td></tr>""";
}
