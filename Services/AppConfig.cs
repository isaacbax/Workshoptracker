namespace WorkshopTracker.Services
{
    public class AppConfig
    {
        public string UsersCsvPath { get; set; } = "";
        public string DataFolder { get; set; } = ""; // Where {branch}open.csv & {branch}closed.csv live
    }
}
