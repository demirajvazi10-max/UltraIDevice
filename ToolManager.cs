using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace UltraIDevice
{
    /// <summary>
    /// Pronalazi i (po potrebi) instalira komandne alate koje aplikacija koristi:
    /// libimobiledevice (idevice_id, ideviceinfo, ideviceenterrecovery,
    /// idevicebackup2), libirecovery (irecovery) i idevicerestore.
    ///
    /// Redosled traženja:
    ///   1. "tools" podfolder pored same aplikacije (ovako dolazi uz instalater)
    ///   2. %LOCALAPPDATA%\UltraIDevice\tools (ovde ih stavlja auto-instalacija)
    ///   3. sistemski PATH (ako ih je korisnik već instalirao)
    ///
    /// Sami alati su open-source (libimobiledevice, LGPL). Na Windows-u im treba i
    /// Apple-ov USB drajver za uređaje — on dolazi uz besplatnu "Apple Devices"
    /// aplikaciju ili uz iTunes. ToolManager to NE instalira, samo proverava.
    /// </summary>
    public static class ToolManager
    {
        // Osnovni alati bez kojih aplikacija ne može ništa korisno.
        public static readonly string[] CoreTools =
        {
            "idevice_id", "ideviceinfo", "idevicerestore", "irecovery",
            "ideviceenterrecovery", "idevicebackup2"
        };

        /// <summary>
        /// Zip sa Windows (x64) binarnim fajlovima alata. Objavljuje ga GitHub Action
        /// (.github/workflows/build-tools.yml) na stalni "tools-latest" release ovog
        /// projekta, pa auto-instalacija radi za sve korisnike bez ičega ručnog. Dok
        /// taj Action ne odradi prvi put, auto-instalacija uredno javi grešku i ponudi
        /// ručno postavljanje — ništa ne puca. Izvor se može promeniti i preko
        /// tools-source.txt (vidi ResolveSource) bez prevođenja.
        /// </summary>
        public const string ToolsBundleUrl =
            "https://github.com/demirajvazi10-max/UltraIDevice/releases/download/tools-latest/ultraidevice-tools-win-x64.zip";

        public static string AppToolsDir =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");

        public static string UserToolsDir =>
            Path.Combine(AppSettings.AppDataFolder, "tools");

        /// <summary>
        /// Opcioni fajl kojim se menja izvor alata bez prevođenja aplikacije.
        /// %LOCALAPPDATA%\UltraIDevice\tools-source.txt može da sadrži:
        ///   • URL do .zip-a sa alatima, ili
        ///   • putanju do lokalnog .zip-a, ili
        ///   • putanju do foldera u kom su .exe/.dll alati.
        /// Ako fajl ne postoji, koristi se podrazumevani ToolsBundleUrl.
        /// </summary>
        public static string SourceFile => Path.Combine(AppSettings.AppDataFolder, "tools-source.txt");

        public static string ResolveSource()
        {
            try
            {
                if (File.Exists(SourceFile))
                {
                    string s = File.ReadAllText(SourceFile).Trim();
                    if (s.Length > 0) return s;
                }
            }
            catch { }
            return ToolsBundleUrl;
        }

        /// <summary>Koji od osnovnih alata još nedostaju (prazno = sve spremno).</summary>
        public static System.Collections.Generic.List<string> MissingTools() =>
            CoreTools.Where(t => Find(t) == null).ToList();

        /// <summary>Puna putanja do alata, ili golo ime ako je na PATH-u, ili null ako ga nema.</summary>
        public static string Find(string tool)
        {
            string exe = tool.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? tool : tool + ".exe";
            foreach (var dir in new[] { AppToolsDir, UserToolsDir })
            {
                string p = Path.Combine(dir, exe);
                if (File.Exists(p)) return p;
            }
            return OnPath(exe) ? tool : null;
        }

        private static bool OnPath(string exe)
        {
            try
            {
                string path = Environment.GetEnvironmentVariable("PATH") ?? "";
                return path.Split(Path.PathSeparator)
                           .Where(d => !string.IsNullOrWhiteSpace(d))
                           .Any(d => { try { return File.Exists(Path.Combine(d.Trim(), exe)); } catch { return false; } });
            }
            catch { return false; }
        }

        /// <summary>Da li su svi osnovni alati dostupni.</summary>
        public static bool ToolsInstalled => CoreTools.All(t => Find(t) != null);

        /// <summary>
        /// Da li je prisutan Apple-ov USB drajver za uređaje. Proverava poznate
        /// servise i foldere; "best effort", nije 100% pouzdano, pa je u UI-ju
        /// samo upozorenje, ne tvrda zabrana.
        /// </summary>
        public static bool AppleDriverPresent()
        {
            // 1. Klasičan servis iz iTunes-a / Apple Mobile Device Support-a.
            foreach (var svc in new[] { "Apple Mobile Device Service", "Apple Mobile Device Process" })
            {
                try
                {
                    using var p = Process.Start(new ProcessStartInfo("sc", $"query \"{svc}\"")
                    {
                        UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true
                    });
                    string outp = p?.StandardOutput.ReadToEnd() ?? "";
                    p?.WaitForExit(3000);
                    if (outp.Contains("STATE", StringComparison.OrdinalIgnoreCase)) return true;
                }
                catch { }
            }

            // 2. Novija "Apple Devices" aplikacija / Apple Mobile Device Support folder.
            try
            {
                string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string pf64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string[] candidates =
                {
                    Path.Combine(pf, "Common Files", "Apple", "Mobile Device Support"),
                    Path.Combine(pf64, "Common Files", "Apple", "Mobile Device Support"),
                };
                if (candidates.Any(Directory.Exists)) return true;
            }
            catch { }

            // 3. Da li uopšte neki Apple proces radi (AppleMobileDeviceProcess, Apple Devices).
            try
            {
                var names = new[] { "applemobiledeviceprocess", "appledevices", "itunes", "amdevicehelper" };
                if (Process.GetProcesses().Any(pr =>
                {
                    try { return names.Contains(pr.ProcessName.ToLowerInvariant()); } catch { return false; }
                })) return true;
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Nabavlja alate i stavlja .exe i .dll u korisnički tools folder. Izvor je
        /// ono što vrati ResolveSource(): URL, lokalni .zip ili lokalni folder.
        /// Baca izuzetak sa jasnom porukom ako ne uspe (MainWindow ga uhvati).
        /// </summary>
        public static async Task InstallToolsAsync(Action<double, string> progress)
        {
            Directory.CreateDirectory(UserToolsDir);
            string source = ResolveSource();

            // 1. Lokalni folder sa već raspakovanim alatima -> samo kopiraj .exe/.dll.
            if (Directory.Exists(source))
            {
                await Task.Run(() =>
                {
                    foreach (var f in Directory.EnumerateFiles(source, "*.*", SearchOption.AllDirectories))
                    {
                        if (f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                            || f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                            File.Copy(f, Path.Combine(UserToolsDir, Path.GetFileName(f)), overwrite: true);
                    }
                });
                progress?.Invoke(100, "");
                return;
            }

            // 2. Lokalni .zip -> raspakuj direktno.
            if (File.Exists(source))
            {
                ExtractToolsZip(source);
                progress?.Invoke(100, "");
                return;
            }

            // 3. Inače je URL -> preuzmi pa raspakuj.
            string zip = Path.Combine(Path.GetTempPath(), $"ultraidevice_tools_{Guid.NewGuid():N}.zip");
            try
            {
                using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
                {
                    http.DefaultRequestHeaders.Add("User-Agent", "UltraIDevice/1.0");
                    using var resp = await http.GetAsync(source, HttpCompletionOption.ResponseHeadersRead);
                    resp.EnsureSuccessStatusCode();
                    long total = resp.Content.Headers.ContentLength ?? -1;
                    await using var src = await resp.Content.ReadAsStreamAsync();
                    await using var dst = File.Create(zip);
                    var buffer = new byte[81920];
                    long done = 0; int read; double last = -1;
                    while ((read = await src.ReadAsync(buffer)) > 0)
                    {
                        await dst.WriteAsync(buffer.AsMemory(0, read));
                        done += read;
                        if (total > 0)
                        {
                            double pct = done * 100.0 / total * 0.9;
                            if (pct - last >= 1) { last = pct; progress?.Invoke(pct, $"{done / 1048576.0:F1} MB"); }
                        }
                    }
                }

                progress?.Invoke(92, "");
                await Task.Run(() => ExtractToolsZip(zip));
                progress?.Invoke(100, "");
            }
            finally { try { File.Delete(zip); } catch { } }
        }

        private static void ExtractToolsZip(string zipPath)
        {
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                if (entry.Length == 0) continue; // folderi
                string name = entry.Name;
                if (!(name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                      || name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))) continue;
                // Ravno u tools folder, bez obzira na strukturu u zip-u.
                entry.ExtractToFile(Path.Combine(UserToolsDir, name), overwrite: true);
            }
        }
    }
}
