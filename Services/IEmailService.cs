namespace Vectrik.Services;

public interface IEmailService
{
    Task SendDemoRequestAsync(DemoRequestEmail request);
    Task SendEmailAsync(string to, string subject, string htmlBody);
}

public class DemoRequestEmail
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Company { get; set; } = "";
    public string? Phone { get; set; }
    public string? MachineCount { get; set; }
    public string? PrimaryProcess { get; set; }
    public string? Message { get; set; }
}
