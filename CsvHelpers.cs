using System;
using System.Collections.Generic;

namespace DesignSheet
{
    public static class CsvHelpers
    {
        public static string[] SplitCsvLine(string line, int expected)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            foreach (char c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString());

            while (result.Count < expected)
                result.Add("");

            return result.ToArray();
        }
    }
}
