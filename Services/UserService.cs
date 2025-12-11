// Services/UserService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WorkshopTracker.Models;

namespace WorkshopTracker.Services
{
    public class UserService
    {
        // Fixed location for admin CSVs
        private const string BaseFolder = @"S:\Public\DesignData\";
        private readonly string _usersPath;

        public string UsersPath => _usersPath;

        public UserService(ConfigService _)
        {
            _usersPath = Path.Combine(BaseFolder, "users.csv");

            var dir = Path.GetDirectoryName(_usersPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // If file missing, create with a default root user
            if (!File.Exists(_usersPath))
            {
                var lines = new[]
                {
                    "username,password,branch",
                    "root,root,headoffice"
                };
                File.WriteAllLines(_usersPath, lines);
            }
        }

        private static string Clean(string? s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Trim().Trim('\"');
        }

        private List<UserRecord> LoadUsers()
        {
            var users = new List<UserRecord>();

            if (!File.Exists(_usersPath))
                return users;

            var lines = File.ReadAllLines(_usersPath);
            if (lines.Length == 0)
                return users;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(',');

                var username = Clean(parts.ElementAtOrDefault(0));
                var password = Clean(parts.ElementAtOrDefault(1));
                var branch = Clean(parts.ElementAtOrDefault(2));

                // Skip header row
                if (username.Equals("username", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrWhiteSpace(username))
                    continue;

                users.Add(new UserRecord
                {
                    Username = username,
                    Password = password,
                    Branch = branch
                });
            }

            return users;
        }

        private void SaveUsers(IEnumerable<UserRecord> users)
        {
            var lines = new List<string> { "username,password,branch" };

            foreach (var u in users)
            {
                lines.Add(string.Join(",", new[]
                {
                    Clean(u.Username),
                    Clean(u.Password),
                    Clean(u.Branch)
                }));
            }

            File.WriteAllLines(_usersPath, lines);
        }

        public IEnumerable<string> GetBranches()
        {
            var branches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // From users.csv
            foreach (var u in LoadUsers())
            {
                if (!string.IsNullOrWhiteSpace(u.Branch))
                    branches.Add(u.Branch);
            }

            // Also from existing *open.csv files (e.g. headofficeopen.csv)
            if (Directory.Exists(BaseFolder))
            {
                foreach (var file in Directory.GetFiles(BaseFolder, "*open.csv"))
                {
                    var name = Path.GetFileNameWithoutExtension(file); // e.g. headofficeopen
                    if (name.EndsWith("open", StringComparison.OrdinalIgnoreCase))
                    {
                        var branch = name.Substring(0, name.Length - "open".Length);
                        if (!string.IsNullOrWhiteSpace(branch))
                            branches.Add(branch);
                    }
                }
            }

            return branches
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .OrderBy(b => b);
        }

        public UserRecord? ValidateLogin(string username, string password)
        {
            var cleanUser = Clean(username);
            var cleanPass = Clean(password);

            var users = LoadUsers();

            // Try actual CSV users first
            var user = users.FirstOrDefault(u =>
                string.Equals(Clean(u.Username), cleanUser, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Clean(u.Password), cleanPass, StringComparison.Ordinal));

            if (user != null)
                return user;

            // Fallback: allow root/root even if CSV parsing is weird
            if (string.Equals(cleanUser, "root", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(cleanPass, "root", StringComparison.Ordinal))
            {
                var firstBranch = GetBranches().FirstOrDefault() ?? "headoffice";
                return new UserRecord
                {
                    Username = "root",
                    Password = "root",
                    Branch = firstBranch
                };
            }

            return null;
        }

        public bool ChangePassword(string username, string newPassword)
        {
            // You said CSV is in an admin folder and change-password is not needed anymore.
            // We keep this stub so nothing crashes, but it does nothing.
            return false;
        }
    }
}
