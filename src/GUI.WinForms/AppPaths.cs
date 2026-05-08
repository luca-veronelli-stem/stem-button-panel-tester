using System;
using System.IO;

namespace GUI.Windows
{
    /// <summary>
    /// Per-user runtime data root: <c>%LOCALAPPDATA%\Stem.ButtonPanel.Tester\</c>.
    /// All file output (logs, extracted resources, probe files) lives here so the
    /// shipped single-file .exe doesn't litter its own directory. Folder name
    /// matches the existing <c>JsonFileDictionaryCache</c> convention so the
    /// dictionary cache and the new artifacts share one location.
    /// </summary>
    internal static class AppPaths
    {
        private const string FolderName = "Stem.ButtonPanel.Tester";

        public static string DataRoot { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            FolderName);

        public static string LogsDir { get; } = Path.Combine(DataRoot, "logs");

        public static string EnsureDataRoot()
        {
            Directory.CreateDirectory(DataRoot);
            return DataRoot;
        }

        public static string EnsureLogsDir()
        {
            Directory.CreateDirectory(LogsDir);
            return LogsDir;
        }
    }
}
