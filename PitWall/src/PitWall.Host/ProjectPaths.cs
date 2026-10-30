using System.IO;

namespace PitWall.Host;

internal static class ProjectPaths
{
    public static string RootDirectory
    {
        get
        {
            string? solutionDirectory = null;
            foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            {
                var directory = new DirectoryInfo(start);
                while (directory is not null)
                {
                    if (File.Exists(Path.Combine(directory.FullName, "Implementation Plan.md")))
                        return directory.FullName;
                    if (solutionDirectory is null && File.Exists(Path.Combine(directory.FullName, "PitWall.sln")))
                        solutionDirectory = directory.FullName;
                    directory = directory.Parent;
                }
            }

            // Installed builds may not have the source tree. Keep their data alongside the app.
            return solutionDirectory ?? AppContext.BaseDirectory;
        }
    }

    public static string RecordingsDirectory => Path.Combine(RootDirectory, "recordings");
    public static string LogsDirectory => Path.Combine(RootDirectory, "logs");
}
