namespace Vectrik.Services;

public class EmailSettings
{
    public string SmtpHost { get; set; } = "mail.privateemail.com";
    public int SmtpPort { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "henry@vectrik.com";
    public string FromName { get; set; } = "Vectrik";
    public string NotificationAddress { get; set; } = "henry@vectrik.com";
    public bool EnableSsl { get; set; } = true;
}
