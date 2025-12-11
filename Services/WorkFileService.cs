using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using WorkshopTracker.Models;

namespace WorkshopTracker.Services
{
    public class WorkFileService
    {
        private readonly ConfigService _config; // kept for ctor signature, not used
        // 🔒 Fixed admin folder for ALL workshop CSVs
        private const string BaseFolder = @"S:\Public\DesignData\";

        // CSV header we will use when we create new files
        private const string Header =
            "RETAIL,OE,CUSTOMER,SERIAL,DAY DUE,DATE DUE,STATUS,QTY,WHAT IS IT,PO,WHAT ARE WE DOING,PARTS,SHAFT,PRIORITY,LAST USER";

        public WorkFileService(ConfigService config)
        {
            _config = config;
        }

        private string GetBaseFolder()
        {
            if (!Directory.Exists(BaseFolder))
            {
                Directory.CreateDirectory(BaseFolder);
            }
            return BaseFolder;
        }

        public string GetFilePath(string branch, bool open)
        {
            var folder = GetBaseFolder();
            var kind = open ? "open" : "closed";
            // e.g. headofficeopen.csv / headofficeclosed.csv
            return Path.Combine(folder, $"{branch}{kind}.csv");
        }

        /// <summary>
        /// If file is missing, create it with just a header line.
        /// Returns the full path.
        /// </summary>
        private string EnsureFileExists(string branch, bool open)
        {
            var path = GetFilePath(branch, open);

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(path))
            {
                File.WriteAllText(path, Header + Environment.NewLine);
            }

            return path;
        }

        public List<WorkRow> LoadWorks(string branch, bool open)
        {
            // Make sure the file exists (creates empty file with header if needed)
            var path = EnsureFileExists(branch, open);

            var lines = File.ReadAllLines(path);

            // If only header, nothing to show
            if (lines.Length <= 1)
                return new List<WorkRow>();

            // Always treat first line as header and skip it
            var dataLines = lines.Skip(1);

            var rows = new List<WorkRow>();

            foreach (var line in dataLines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var p = line.Split(',');

                // DATE DUE (index 5) – try dd/MM/yyyy first, then fallback to generic parse
                DateTime? date = null;
                var dateRaw = (p.Length > 5 ? p[5] : string.Empty)?.Trim();
                if (!string.IsNullOrEmpty(dateRaw))
                {
                    if (DateTime.TryParseExact(dateRaw, "dd/MM/yyyy",
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out var d1))
                    {
                        date = d1;
                    }
                    else if (DateTime.TryParse(dateRaw, out var d2))
                    {
                        date = d2;
                    }
                }

                // QTY (index 7)
                int qty = 0;
                if (p.Length > 7)
                    int.TryParse(p[7], out qty);

                rows.Add(new WorkRow
                {
                    Retail = p.ElementAtOrDefault(0) ?? string.Empty,
                    OE = p.ElementAtOrDefault(1) ?? string.Empty,
                    Customer = p.ElementAtOrDefault(2) ?? string.Empty,
                    Serial = p.ElementAtOrDefault(3) ?? string.Empty,
                    DayDue = p.ElementAtOrDefault(4) ?? string.Empty,
                    DateDue = date,
                    Status = p.ElementAtOrDefault(6) ?? string.Empty,
                    Qty = qty,
                    WhatIsIt = p.ElementAtOrDefault(8) ?? string.Empty,
                    PO = p.ElementAtOrDefault(9) ?? string.Empty,
                    WhatAreWeDoing = p.ElementAtOrDefault(10) ?? string.Empty,
                    Parts = p.ElementAtOrDefault(11) ?? string.Empty,
                    Shaft = p.ElementAtOrDefault(12) ?? string.Empty,
                    Priority = p.ElementAtOrDefault(13) ?? string.Empty,
                    LastUser = p.ElementAtOrDefault(14) ?? string.Empty
                });
            }

            return GroupByDate(rows);
        }

        public void SaveWorks(string branch, bool open, IEnumerable<WorkRow> rows, string currentUser)
        {
            var path = EnsureFileExists(branch, open);

            // Skip group rows and ensure LastUser is populated
            var flatRows = rows
                .Where(r => !r.IsGroupRow)
                .OrderBy(r => r.DateDue ?? DateTime.MinValue)
                .ToList();

            var lines = new List<string> { Header };

            foreach (var r in flatRows)
            {
                r.LastUser = currentUser;
                var dateStr = r.DateDue?.ToString("dd/MM/yyyy") ?? string.Empty;

                lines.Add(string.Join(",", new[]
                {
                    r.Retail,
                    r.OE,
                    r.Customer,
                    r.Serial,
                    r.DayDue,
                    dateStr,
                    r.Status,
                    r.Qty.ToString(),
                    r.WhatIsIt,
                    r.PO,
                    r.WhatAreWeDoing,
                    r.Parts,
                    r.Shaft,
                    r.Priority,
                    r.LastUser
                }));
            }

            File.WriteAllLines(path, lines);
        }

        /// <summary>
        /// Insert uneditable group rows between dates.
        /// </summary>
        public List<WorkRow> GroupByDate(IEnumerable<WorkRow> flatRows)
        {
            var rows = new List<WorkRow>();

            var ordered = flatRows
                .OrderBy(r => r.DateDue ?? DateTime.MinValue)
                .ToList();

            foreach (var group in ordered.GroupBy(r => r.DateDue?.Date))
            {
                var date = group.Key;

                rows.Add(new WorkRow
                {
                    IsGroupRow = true,
                    DateDue = date,
                    Customer = date.HasValue ? date.Value.ToString("dd/MM/yyyy") : "No Date"
                });

                rows.AddRange(group);
            }

            return rows;
        }
    }
}
