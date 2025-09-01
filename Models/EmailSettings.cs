namespace BackupApp.Models
{
    public class EmailSettings
    {
        public string SmtpServer { get; set; } = "smtp.aol.com";
        public int SmtpPort { get; set; } = 587;
        public string Username { get; set; } = "dzekn@aol.com";
        public string Password { get; set; } = "ibewcjgpnjxketvi"; // App password
        public string FromEmail { get; set; } = "dzekn@aol.com";
        public string ToEmail { get; set; } = "dzekn@aol.com";
        public bool EnableSsl { get; set; } = true;

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(SmtpServer) &&
                   !string.IsNullOrEmpty(Username) &&
                   !string.IsNullOrEmpty(Password) &&
                   !string.IsNullOrEmpty(FromEmail) &&
                   !string.IsNullOrEmpty(ToEmail);
        }
    }
}
