using System.IO;

namespace WorkshopTracker.Services
{
    public class ConfigService
    {
        // 🔒 Fixed base folder for ALL CSVs
        private const string BaseFolderConst = @"S:\Public\DesignData\";

        public AppConfig Config { get; }

        public ConfigService()
        {
            // Ensure base folder exists
            if (!Directory.Exists(BaseFolderConst))
            {
                Directory.CreateDirectory(BaseFolderConst);
            }

            // Always use this users.csv
            var usersPath = Path.Combine(BaseFolderConst, "users.csv");

            // Ensure users.csv exists with header if not present
            if (!File.Exists(usersPath))
            {
                // Header: username,password,branch
                File.WriteAllText(usersPath, "username,password,branch" + System.Environment.NewLine);
            }

            // ✅ Use your existing AppConfig class (defined elsewhere)
            Config = new AppConfig
            {
                UsersCsvPath = usersPath,
                DataFolder = BaseFolderConst
            };
        }

        // Kept so other code compiles, but it's effectively a no-op now.
        public void Save()
        {
            // Config is fixed & not persisted to disk anymore.
        }

        public static string BaseFolder => BaseFolderConst;
    }
}
