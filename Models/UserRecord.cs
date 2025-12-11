namespace DesignSheet.Models
{
    public class UserRecord
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string Branch { get; set; } = "";

        // Always use this for file names
        public string BranchClean => (Branch ?? string.Empty).Trim();
    }
}
