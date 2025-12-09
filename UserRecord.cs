namespace DesignSheet
{
    public class UserRecord
    {
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Branch { get; set; } = "headoffice";
    }
}
