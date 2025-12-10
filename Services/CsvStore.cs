using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DesignSheet.Models;

namespace DesignSheet.Services;

public sealed class CsvStore
{
    // ---------- USERS ----------
    public UserRecord[] LoadUsers(string dataFolder)
    {
        var p = Paths.UsersCsv(dataFolder);
        if (!File.Exists(p)) return Array.Empty<UserRecord>();

        var rows = ReadCsv(p);
        if (rows.Count < 2) return Array.Empty<UserRecord>();

        var header = rows[0].Select(h => h.Trim()).ToArray();

        int iu = IndexOf(header, "username");
        int ip = IndexOf(header, "password");
        int ib = IndexOf(header, "branch");
        if (iu < 0 || ip < 0 || ib < 0)
        {
            // Header not found in expected form
            return Array.Empty<UserRecord>();
        }

        var list = new List<UserRecord>();
        for (int i = 1; i < rows.Count; i++)
        {
            var r = rows[i];
            if (r.Length == 0) continue;

            var username = Get(r, iu).Trim();
            var password = Get(r, ip).Trim();
            var branch = Get(r, ib).Trim();

            if (string.IsNullOrEmpty(username))
                continue;

            list.Add(new UserRecord
            {
                Username = username,
                Password = password,
                Branch = branch
            });
        }
        return list.ToArray();
    }

    public void SaveUsers(string dataFolder, IEnumerable<UserRecord> users)
    {
        var p = Paths.UsersCsv(dataFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);

        var lines = new List<string> { "username,password,branch" };
        lines.AddRange(users.Select(u =>
            $"{Escape(u.Username)},{Escape(u.Password)},{Escape(u.Branch)}"));

        File.WriteAllLines(p, lines, Encoding.UTF8);
    }

    // ---------- WORK ROWS ----------

    public WorkRow[] LoadWork(string csvPath)
    {
        if (!File.Exists(csvPath)) return Array.Empty<WorkRow>();

        var rows = ReadCsv(csvPath);
        if (rows.Count < 2) return Array.Empty<WorkRow>();

        var header = rows[0].Select(h => h.Trim()).ToArray();

        int iRetail = IndexOf(header, "RETAIL");
        int iOe = IndexOf(header, "OE");
        int iCustomer = IndexOf(header, "CUSTOMER");
        int iSerial = IndexOf(header, "SERIAL");
        int iDayDue = IndexOf(header, "DAY DUE", "DAY_DUE");
        int iDateDue = IndexOf(header, "DATE DUE", "DATE_DUE");
        int iStatus = IndexOf(header, "STATUS");
        int iQty = IndexOf(header, "QTY");
        int iWhatIsIt = IndexOf(header, "WHAT IS IT", "WHAT_IS_IT");
        int iPo = IndexOf(header, "PO");
        int iWhatDoing = IndexOf(header, "WHAT ARE WE DOING", "WHAT_ARE_WE_DOING");
        int iParts = IndexOf(header, "PARTS");
        int iShaft = IndexOf(header, "SHAFT");
        int iPriority = IndexOf(header, "PRIORITY");
        int iLastUser = IndexOf(header, "LAST USER", "LAST_USER");

        var result = new List<WorkRow>();
        for (int i = 1; i < rows.Count; i++)
        {
            var r = rows[i];
            if (r.Length == 0) continue;

            result.Add(new WorkRow
            {
                RETAIL = Get(r, iRetail).Trim(),
                OE = Get(r, iOe).Trim(),
                CUSTOMER = Get(r, iCustomer).Trim(),
                SERIAL = Get(r, iSerial).Trim(),
                DAY_DUE = Get(r, iDayDue).Trim(),
                DATE_DUE = Get(r, iDateDue).Trim(),
                STATUS = Get(r, iStatus).Trim(),
                QTY = Get(r, iQty).Trim(),
                WHAT_IS_IT = Get(r, iWhatIsIt).Trim(),
                PO = Get(r, iPo).Trim(),
                WHAT_ARE_WE_DOING = Get(r, iWhatDoing).Trim(),
                PARTS = Get(r, iParts).Trim(),
                SHAFT = Get(r, iShaft).Trim(),
                PRIORITY = Get(r, iPriority).Trim(),
                LAST_USER = Get(r, iLastUser).Trim()
            });
        }

        return result.ToArray();
    }

    public void SaveWork(string csvPath, IEnumerable<WorkRow> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(csvPath)!);

        var header = new[]
        {
            "RETAIL","OE","CUSTOMER","SERIAL","DAY DUE","DATE DUE","STATUS",
            "QTY","WHAT IS IT","PO","WHAT ARE WE DOING","PARTS","SHAFT","PRIORITY","LAST USER"
        };

        var lines = new List<string> { string.Join(",", header) };
        foreach (var r in rows)
        {
            lines.Add(string.Join(",",
                Escape(r.RETAIL),
                Escape(r.OE),
                Escape(r.CUSTOMER),
                Escape(r.SERIAL),
                Escape(r.DAY_DUE),
                Escape(r.DATE_DUE),
                Escape(r.STATUS),
                Escape(r.QTY),
                Escape(r.WHAT_IS_IT),
                Escape(r.PO),
                Escape(r.WHAT_ARE_WE_DOING),
                Escape(r.PARTS),
                Escape(r.SHAFT),
                Escape(r.PRIORITY),
                Escape(r.LAST_USER)
            ));
        }

        File.WriteAllLines(csvPath, lines, Encoding.UTF8);
    }

    public void Backup(string csvPath)
    {
        if (!File.Exists(csvPath)) return;
        var dir = Path.GetDirectoryName(csvPath)!;
        var name = Path.GetFileNameWithoutExtension(csvPath);
        var ext = Path.GetExtension(csvPath);
        var backup = Path.Combine(dir, $"{name}_backup_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
        File.Copy(csvPath, backup, overwrite: false);
    }

    // ---------- CSV helpers ----------

    private static List<string[]> ReadCsv(string path)
    {
        var all = File.ReadAllLines(path, Encoding.UTF8);
        var list = new List<string[]>(all.Length);

        foreach (var line in all)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            list.Add(ParseLine(line));
        }

        return list;
    }

    private static string[] ParseLine(string line)
    {
        // Support both comma and semicolon separators (Excel regional settings)
        char separator;
        if (line.Contains(';') && !line.Contains(','))
            separator = ';';
        else
            separator = ',';

        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else
            {
                if (c == separator)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        result.Add(sb.ToString());
        return result.ToArray();
    }

    private static string Escape(string? s)
    {
        s ??= "";
        if (s.Contains('"')) s = s.Replace("\"", "\"\"");
        if (s.Contains(',') || s.Contains(';') || s.Contains('\n') || s.Contains('\r') || s.Contains('"'))
            return $"\"{s}\"";
        return s;
    }

    private static int IndexOf(string[] header, params string[] names)
    {
        for (int i = 0; i < header.Length; i++)
        {
            foreach (var n in names)
                if (string.Equals(header[i], n, StringComparison.OrdinalIgnoreCase))
                    return i;
        }
        return -1;
    }

    private static string Get(string[] row, int idx) =>
        (idx >= 0 && idx < row.Length) ? row[idx] : "";
}
