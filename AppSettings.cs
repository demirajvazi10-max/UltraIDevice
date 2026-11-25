using System;
using System.IO;
using System.Text.Json;

namespace UltraIDevice
{
    /// <summary>
    /// Sva podešavanja na jednom mestu, u settings.json u AppData folderu.
    /// Čuva se ODMAH pri svakoj izmeni (ne tek pri izlazu). Namerno malo —
    /// Ultra iDevice nema puno trajnih opcija, samo izgled, jezik i zadnje
    /// korišćene foldere (za backup i za IPSW fajlove).
    /// </summary>
    public class AppSettings
    {
        // ── Izgled ──
        public string Theme { get; set; } = "";       // "", dark, light, contrast
        public string Language { get; set; } = "";     // "", en, sr

        // ── Zapamćeni folderi ──
        public string BackupFolder { get; set; } = "";     // gde se čuvaju bekapi
        public string LastIpswFolder { get; set; } = "";    // gde korisnik drži .ipsw fajlove

        // ── Ponašanje ──
        public bool SpeakProgress { get; set; } = true;     // govorne najave koraka i procenata
        public bool AutoRefreshDevice { get; set; } = true; // sam proveravaj da li je telefon povezan
        public bool KeepFirmware { get; set; } = false;     // čuvati preuzeti IPSW (10+ GB) posle instalacije? podrazumevano NE

        public static string AppDataFolder { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UltraIDevice");

        public static string DefaultBackupFolder { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "UltraIDevice_Backups");

        private static string FilePath => Path.Combine(AppDataFolder, "settings.json");

        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

        public static AppSettings Load()
        {
            AppSettings s;
            try
            {
                s = File.Exists(FilePath)
                    ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings()
                    : new AppSettings();
            }
            catch { s = new AppSettings(); }

            if (string.IsNullOrWhiteSpace(s.BackupFolder)) s.BackupFolder = DefaultBackupFolder;
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(AppDataFolder);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(this, JsonOpts));
                File.Move(tmp, FilePath, true);
            }
            catch { /* podešavanja nisu kritična — nikad ne ruši aplikaciju zbog njih */ }
        }
    }
}
