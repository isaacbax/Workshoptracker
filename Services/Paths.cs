using System.IO;

namespace DesignSheet.Services;

public static class Paths
{
    public static string UsersCsv(string dataFolder) =>
        Path.Combine(dataFolder, "users.csv");

    public static string OpenCsv(string dataFolder, string branch) =>
        Path.Combine(dataFolder, $"{branch}open.csv");

    public static string ClosedCsv(string dataFolder, string branch) =>
        Path.Combine(dataFolder, $"{branch}closed.csv");
}
