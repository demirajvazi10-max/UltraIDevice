using System;
using System.IO;

namespace UltraIDevice
{
    /// <summary>
    /// Jednostavan log: zapisuje sirov izlaz alata u dnevni fajl u
    /// %LOCALAPPDATA%\UltraIDevice\logs. Koristi se da bismo videli ŠTA tačno
    /// alati ispisuju na pravom uređaju, pa da doteramo parsiranje. Nikad ne ruši
    /// aplikaciju ako pisanje ne uspe.
    /// </summary>
    public static class Logger
    {
        public static string LogsDir { get; } = Path.Combine(AppSettings.AppDataFolder, "logs");

        private static readonly object Gate = new();

        public static void Write(string line)
        {
            if (line == null) return;
            try
            {
                Directory.CreateDirectory(LogsDir);
                string file = Path.Combine(LogsDir, $"ultraidevice-{DateTime.Now:yyyy-MM-dd}.log");
                lock (Gate)
                    File.AppendAllText(file, $"{DateTime.Now:HH:mm:ss}  {line}{Environment.NewLine}");
            }
            catch { }
        }

        /// <summary>Upis zaglavlja pre nove radnje, da se u logu jasno vidi gde počinje šta.</summary>
        public static void Section(string title) => Write($"==================== {title} ====================");
    }
}
