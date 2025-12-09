using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace DesignSheet
{
    public static class AppConfig
    {
        // Root folder that holds the CSV files and users.csv
        public static string BaseFolder { get; set; } =
            @"S:\Public\DesignData";

        public static string UsersFile =>
            Path.Combine(BaseFolder, "users.csv");

        // These are set during login and used by MainWindow
        public static string CurrentUserName { get; set; } = string.Empty;
        public static string CurrentBranch { get; set; } = "headoffice";

        /// <summary>
        /// Save users back to users.csv
        /// </summary>
        public static void SaveUsers(IReadOnlyCollection<UserRecord> users)
        {
            try
            {
                Directory.CreateDirectory(BaseFolder);
                var lines = new List<string>();

                foreach (var u in users)
                {
                    lines.Add($"{u.Username},{u.PasswordHash},{u.Branch}");
                }

                File.WriteAllLines(UsersFile, lines);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving users: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Load users from users.csv
        /// </summary>
        public static List<UserRecord> LoadUsers()
        {
            var users = new List<UserRecord>();

            try
            {
                if (!File.Exists(UsersFile))
                    return users;

                foreach (var line in File.ReadAllLines(UsersFile))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = line.Split(',');
                    if (parts.Length < 3)
                        continue;

                    users.Add(new UserRecord
                    {
                        Username = parts[0].Trim(),
                        PasswordHash = parts[1].Trim(),
                        Branch = parts[2].Trim()
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading users: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return users;
        }

        /// <summary>
        /// Exists because MainWindow calls AppConfig.SaveSettings() on close.
        /// Implement persistence here later if you want.
        /// </summary>
        public static void SaveSettings()
        {
            // No-op: add settings persistence here if desired (json/ini/etc).
        }

        /// <summary>
        /// Change the base folder (called from Login window).
        /// </summary>
        public static void ChangeRootFolder(Window owner)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select the folder that contains the branch CSV files"
            };

            var result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK &&
                !string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                BaseFolder = dialog.SelectedPath;
            }
        }
    }
}
