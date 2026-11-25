using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace UltraIDevice
{
    /// <summary>
    /// Sve što komunicira sa iPhone/iPad uređajem preko komandnih alata
    /// (libimobiledevice + idevicerestore). Bez UI-ja. MainWindow poziva ove
    /// metode i prikazuje/izgovara ono što one jave kroz "onProgress".
    ///
    /// NAPOMENA o pouzdanosti: tačan izlaz alata (koje redove i procente ispisuju)
    /// zavisi od verzije alata i modela uređaja, pa je parsiranje napretka "best
    /// effort". Uspeh/neuspeh se na kraju ODLUČUJE po izlaznom kodu procesa, ne po
    /// pogođenom procentu. Ovo je mesto koje ćemo najviše dorađivati uz prave probe.
    /// </summary>
    public static class DeviceEngine
    {
        /// <summary>
        /// Opciona kuka: ako je postavljena, dobija SVAKI red izlaza svih alata
        /// (i stdout i stderr). MainWindow je veže na Logger, pa imamo pun, sirov
        /// zapis stvarnog ponašanja alata — neophodno da kasnije doteramo parsiranje
        /// napretka i prepoznavanje grešaka prema pravim probama.
        /// </summary>
        public static Action<string> LogSink;

        /// <summary>
        /// Da li posle instalacije ZADRŽATI preuzeti firmver (IPSW) i raspakovane
        /// fajlove. Podrazumevano false — brišu se, jer su ogromni (10+ GB). MainWindow
        /// postavlja ovo iz podešavanja na startu.
        /// </summary>
        public static bool KeepFirmware = false;

        /// <summary>Folder u koji idevicerestore preuzima firmver i kešira raspakovane fajlove.</summary>
        public static string FirmwareDir => System.IO.Path.Combine(AppSettings.AppDataFolder, "firmware");

        /// <summary>Jedna najava napretka ka UI-ju.</summary>
        public sealed class Progress
        {
            public string Step = "";     // tekući korak ("Sending filesystem", "Verifying"...)
            public double Percent = -1;    // -1 = nepoznato
        }

        // ════════════════════════════════════════════════════════════════
        // DETEKCIJA UREĐAJA
        // ════════════════════════════════════════════════════════════════
        public static async Task<DeviceInfo> DetectAsync(CancellationToken ct = default)
        {
            // 1. Normalan mod: idevice_id -l vraća UDID-ove, jedan po redu.
            string idTool = ToolManager.Find("idevice_id");
            if (idTool != null)
            {
                var sb = new StringBuilder();
                await RunWithTimeoutAsync(idTool, new[] { "-l" }, ct, 8, sb);
                string udid = sb.ToString()
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .FirstOrDefault(l => l.Length > 0);
                if (!string.IsNullOrEmpty(udid))
                {
                    var info = new DeviceInfo { Udid = udid, Mode = DeviceMode.Normal };
                    await FillInfoAsync(info, ct);
                    return info;
                }
            }

            // 2. Recovery / DFU: irecovery -q ispisuje podatke o uređaju u tim modovima.
            string rec = ToolManager.Find("irecovery");
            if (rec != null)
            {
                var sb = new StringBuilder();
                int code = await RunWithTimeoutAsync(rec, new[] { "-q" }, ct, 8, sb, sb);
                string outp = sb.ToString();
                if (code == 0 && outp.Trim().Length > 0)
                {
                    var info = new DeviceInfo { Mode = DeviceMode.Recovery };
                    if (Regex.IsMatch(outp, @"\bDFU\b", RegexOptions.IgnoreCase)) info.Mode = DeviceMode.Dfu;
                    // Model iz CPID/MODEL polja ako postoji (npr. "MODEL: d84ap").
                    var m = Regex.Match(outp, @"PRODUCT:\s*(\S+)", RegexOptions.IgnoreCase);
                    if (m.Success) info.ProductType = m.Groups[1].Value.Trim();
                    return info;
                }
            }

            return new DeviceInfo { Mode = DeviceMode.None };
        }

        /// <summary>Popunjava model, iOS verziju, serijski broj i ime (samo u normalnom modu).</summary>
        private static async Task FillInfoAsync(DeviceInfo info, CancellationToken ct)
        {
            string tool = ToolManager.Find("ideviceinfo");
            if (tool == null) return;
            info.ProductType   = await InfoKeyAsync(tool, info.Udid, "ProductType", ct);
            info.ProductVersion = await InfoKeyAsync(tool, info.Udid, "ProductVersion", ct);
            info.SerialNumber  = await InfoKeyAsync(tool, info.Udid, "SerialNumber", ct);
            info.DeviceName    = await InfoKeyAsync(tool, info.Udid, "DeviceName", ct);
        }

        private static async Task<string> InfoKeyAsync(string tool, string udid, string key, CancellationToken ct, string domain = null)
        {
            var args = new List<string> { "-u", udid };
            if (!string.IsNullOrEmpty(domain)) { args.Add("-q"); args.Add(domain); }
            args.Add("-k"); args.Add(key);
            var sb = new StringBuilder();
            await RunWithTimeoutAsync(tool, args.ToArray(), ct, 8, sb);
            return sb.ToString().Trim();
        }

        /// <summary>
        /// Pun izveštaj o uređaju (baterija, memorija, mrežne adrese, aktivacija...).
        /// Puni se tek na zahtev (dugme Informacije), ne pri svakom osvežavanju, da
        /// osnovna detekcija ostane brza.
        /// </summary>
        public static async Task<DeviceInfo> GetFullReportAsync(string udid, CancellationToken ct = default)
        {
            var info = new DeviceInfo { Udid = udid, Mode = DeviceMode.Normal };
            await FillInfoAsync(info, ct);
            await FillExtendedAsync(info, ct);
            return info;
        }

        private static async Task FillExtendedAsync(DeviceInfo info, CancellationToken ct)
        {
            string tool = ToolManager.Find("ideviceinfo");
            if (tool == null) return;

            info.BuildVersion     = await InfoKeyAsync(tool, info.Udid, "BuildVersion", ct);
            info.ModelNumber      = await InfoKeyAsync(tool, info.Udid, "ModelNumber", ct);
            info.RegionInfo       = await InfoKeyAsync(tool, info.Udid, "RegionInfo", ct);
            info.CpuArchitecture  = await InfoKeyAsync(tool, info.Udid, "CPUArchitecture", ct);
            info.WiFiAddress      = await InfoKeyAsync(tool, info.Udid, "WiFiAddress", ct);
            info.BluetoothAddress = await InfoKeyAsync(tool, info.Udid, "BluetoothAddress", ct);
            info.PhoneNumber      = await InfoKeyAsync(tool, info.Udid, "PhoneNumber", ct);
            info.ActivationState  = await InfoKeyAsync(tool, info.Udid, "ActivationState", ct);

            // Baterija (poseban domen).
            string cap = await InfoKeyAsync(tool, info.Udid, "BatteryCurrentCapacity", ct, "com.apple.mobile.battery");
            if (int.TryParse(cap.Trim(), out int pct)) info.BatteryPercent = pct;
            string chg = await InfoKeyAsync(tool, info.Udid, "BatteryIsCharging", ct, "com.apple.mobile.battery");
            info.BatteryCharging = chg.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);

            // Memorija (poseban domen), vrednosti u bajtovima.
            string tot = await InfoKeyAsync(tool, info.Udid, "TotalDiskCapacity", ct, "com.apple.disk_usage");
            string free = await InfoKeyAsync(tool, info.Udid, "TotalDataAvailable", ct, "com.apple.disk_usage");
            if (long.TryParse(tot.Trim(), out long t)) info.StorageTotalBytes = t;
            if (long.TryParse(free.Trim(), out long f)) info.StorageFreeBytes = f;
        }

        // ════════════════════════════════════════════════════════════════
        // UPDATE / RESTORE  (idevicerestore)
        // ════════════════════════════════════════════════════════════════
        /// <summary>
        /// Update (čuva podatke) ili restore (briše). latestSigned=true preuzima
        /// najnoviji potpisani iOS; inače se koristi ipswPath. Baca izuzetak sa
        /// porukom (i kodom greške kad ga ima) ako proces ne uspe.
        /// </summary>
        public static async Task RunRestoreAsync(bool erase, bool latestSigned, string ipswPath,
            Action<Progress> onProgress, CancellationToken ct)
        {
            string tool = ToolManager.Find("idevicerestore")
                ?? throw new InvalidOperationException("idevicerestore not found");

            // Firmver i raspakovani fajlovi idu u naš folder, da ih posle očistimo.
            string fwDir = FirmwareDir;
            try { System.IO.Directory.CreateDirectory(fwDir); } catch { }

            var args = new List<string>();
            // VAŽNO: idevicerestore NEMA opciju --update. Update (bez brisanja) je
            // podrazumevano ponašanje — ne prosleđuje se nikakav fleg. Samo --erase
            // za pun restore sa brisanjem. (Pogrešan --update je ranije izazivao da
            // idevicerestore ispiše "usage" i javi grešku.)
            if (erase) args.Add("--erase");
            if (latestSigned) args.Add("--latest");
            args.Add("--cache-path"); args.Add(fwDir);

            // Pozicioni IPSW ide na kraj (idevicerestore ga zahteva ako nije --latest).
            if (!latestSigned)
            {
                if (string.IsNullOrWhiteSpace(ipswPath))
                    throw new InvalidOperationException("No IPSW file selected.");
                args.Add(ipswPath);
            }

            var prog = new Progress();
            var err = new StringBuilder();
            int code;
            try
            {
                // WorkingDirectory = fwDir: --latest preuzima IPSW baš tu, pa ga lako obrišemo.
                code = await RunAsync(tool, args, line =>
                {
                    if (ParseRestoreLine(line, prog)) onProgress?.Invoke(prog);
                }, ct, null, err, workingDir: fwDir);
            }
            finally
            {
                // Obriši preuzeti firmver i raspakovane fajlove (osim ako korisnik želi da ih čuva).
                // NIKAD ne dira korisnikov sopstveni IPSW — on je van ovog foldera.
                if (!KeepFirmware) CleanFirmware(fwDir);
            }

            if (code != 0)
                throw new RestoreException(code, err.ToString());
        }

        /// <summary>
        /// Tumači red izlaza idevicerestore-a; true = napredak se promenio.
        /// p.Step dobija STABILAN KLJUČ faze (npr. "download", "filesystem"), koji
        /// MainWindow prevodi i izgovara. Procenat prikazujemo samo za duge, monotone
        /// faze; za brze faze sa mnogo komponenti (slanje firmvera) procenat bi stalno
        /// skakao 0-100 po komponenti, pa ga tu ne diramo.
        /// </summary>
        public static bool ParseRestoreLine(string line, Progress p)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            bool changed = false;

            string linePhase = PhaseKey(line);

            // Naziv faze napreduje SAMO unapred (po rangu). idevicerestore ume da
            // izmeša redove raznih faza — npr. "Sending <component>" usred slanja
            // fajl-sistema — pa bez ovoga bi naziv (i traka) stalno skakali nazad.
            if (linePhase != null && Rank(linePhase) > Rank(p.Step))
            {
                p.Step = linePhase;
                p.Percent = PercentPhases.Contains(linePhase) ? 0 : -1;  // nova duga faza kreće od 0
                changed = true;
            }

            // Procenat čitamo SAMO iz reda koji stvarno pripada dugoj, monotonoj fazi
            // (preuzimanje, provera, fajl-sistem, sistem/Cryptex). Tako procenti
            // pojedinačnih komponenti ("Ap,...: 50%", "Uploading: 50%") ne teraju traku.
            if (linePhase != null && PercentPhases.Contains(linePhase))
            {
                var m = Regex.Match(line, @"(\d{1,3}(?:\.\d+)?)\s*%");
                if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double pct))
                {
                    pct = Math.Clamp(pct, 0, 100);
                    if (Math.Abs(pct - p.Percent) >= 1) { p.Percent = pct; changed = true; }
                }
            }

            return changed;
        }

        /// <summary>
        /// Mapira sirov red izlaza na stabilan ključ faze. Redosled provera je bitan:
        /// specifičnije ("Uploading filesystem") mora pre opštijeg ("Uploading").
        /// Izmešane/kratke radnje (slanje komponenti, baseband, personalizacija) svode
        /// se na jednu fazu "send" — niži rang, pa ih duga faza fajl-sistema ne obara.
        /// </summary>
        private static string PhaseKey(string line)
        {
            bool C(string s) => line.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0;

            if (C("Downloading")) return "download";
            if (C("Verifying filesystem") || C("Verifying restore")) return "filesystem";
            if (C("Verifying")) return "verify";
            if (C("Uploading filesystem") || C("Sending filesystem") || C("Restoring image")
                || C("Creating filesystem") || C("Creating partition") || C("Mounting")) return "filesystem";
            if (C("Cryptex")) return "cryptex";
            if (C("Installing") || C("Sealing") || C("Fixing up")
                || C("Restore Finished") || C("DONE") || C("Rebooting")) return "finishing";
            if (C("Entering recovery") || C("entered restore mode")) return "recovery";
            if (C("Uploading") || C("Sending") || C("Personalizing") || C("Flashing")
                || C("Baseband") || C("Updating")) return "send";
            if (C("Extracting") || C("Checking") || C("Preparing") || C("Requesting") || C("Getting")) return "prepare";
            return null;
        }

        /// <summary>Redosled faza (veći = kasnije). Služi da naziv faze ide samo unapred.</summary>
        private static int Rank(string phase) => phase switch
        {
            "prepare" => 10,
            "download" => 20,
            "verify" => 30,
            "recovery" => 40,
            "send" => 50,
            "filesystem" => 60,
            "cryptex" => 70,
            "finishing" => 90,
            _ => 0,   // "" / nepoznato
        };

        // Faze za koje prikazujemo procenat (duge i monotone, pa je % smislen).
        private static readonly HashSet<string> PercentPhases = new(StringComparer.Ordinal)
        {
            "download", "verify", "filesystem", "cryptex"
        };

        // ════════════════════════════════════════════════════════════════
        // RECOVERY MOD
        // ════════════════════════════════════════════════════════════════
        public static async Task EnterRecoveryAsync(string udid, CancellationToken ct)
        {
            string tool = ToolManager.Find("ideviceenterrecovery")
                ?? throw new InvalidOperationException("ideviceenterrecovery not found");
            if (string.IsNullOrEmpty(udid))
                throw new InvalidOperationException("No device UDID.");
            var err = new StringBuilder();
            int code = await RunAsync(tool, new[] { udid }, null, ct, null, err);
            if (code != 0)
                throw new InvalidOperationException(LastError(err.ToString()) ?? "Could not enter recovery mode.");
        }

        /// <summary>
        /// Izlazak iz recovery/DFU moda u normalno pokretanje. Različite verzije
        /// irecovery-ja se razlikuju, pa pokušavamo nekoliko bezbednih komandi.
        /// </summary>
        public static async Task ExitRecoveryAsync(CancellationToken ct)
        {
            string tool = ToolManager.Find("irecovery")
                ?? throw new InvalidOperationException("irecovery not found");

            // Postavi auto-boot i restartuj. Ako setenv nije dozvoljen na uređaju,
            // obična "reboot" komanda i dalje izlazi iz recovery-ja na većini modela.
            await RunAsync(tool, new[] { "-c", "setenv auto-boot true" }, null, ct);
            await RunAsync(tool, new[] { "-c", "saveenv" }, null, ct);
            var err = new StringBuilder();
            int code = await RunAsync(tool, new[] { "-c", "reboot" }, null, ct, null, err);
            if (code != 0)
            {
                // Rezerva: neki build-ovi koriste -n za normalno pokretanje.
                code = await RunAsync(tool, new[] { "-n" }, null, ct, null, err);
                if (code != 0)
                    throw new InvalidOperationException(LastError(err.ToString()) ?? "Could not exit recovery mode.");
            }
        }

        // ════════════════════════════════════════════════════════════════
        // APLIKACIJE (ideviceinstaller): lista / instalacija / deinstalacija
        // ════════════════════════════════════════════════════════════════
        public sealed class InstalledApp
        {
            public string BundleId = "";
            public string Name = "";
            public string Version = "";
        }

        /// <summary>Lista instaliranih (korisničkih) aplikacija.</summary>
        public static async Task<List<InstalledApp>> ListAppsAsync(string udid, CancellationToken ct)
        {
            string tool = ToolManager.Find("ideviceinstaller")
                ?? throw new InvalidOperationException("ideviceinstaller not found");
            if (string.IsNullOrEmpty(udid)) throw new InvalidOperationException("No device UDID.");

            var so = new StringBuilder();
            var se = new StringBuilder();
            int code = await RunAsync(tool, new[] { "-u", udid, "list" }, null, ct, so, se);
            if (code != 0 && so.Length == 0)
                throw new InvalidOperationException(LastError(se.ToString()) ?? "Could not list apps.");

            var apps = new List<InstalledApp>();
            foreach (var raw in so.ToString().Split('\n'))
            {
                string line = raw.Trim().Trim('\r');
                if (line.Length == 0) continue;
                // Preskoči zaglavlje/sažetak raznih verzija alata.
                if (line.StartsWith("Total:", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.StartsWith("CFBundleIdentifier", StringComparison.OrdinalIgnoreCase)) continue;

                var app = ParseAppLine(line);
                if (app != null) apps.Add(app);
            }
            apps.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return apps;
        }

        // Tolerantno tumačenje reda. Podržava i "id, \"ver\", \"Ime\"" i "id ver Ime".
        private static InstalledApp ParseAppLine(string line)
        {
            if (line.IndexOf(',') >= 0)
            {
                var parts = line.Split(new[] { ',' }, 3);
                string id = parts[0].Trim();
                if (!id.Contains(".")) return null;
                string ver = parts.Length > 1 ? parts[1].Trim().Trim('"') : "";
                string name = parts.Length > 2 ? parts[2].Trim().Trim('"') : id;
                return new InstalledApp { BundleId = id, Version = ver, Name = string.IsNullOrEmpty(name) ? id : name };
            }
            else
            {
                var parts = line.Split(new[] { ' ', '\t' }, 3, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0 || !parts[0].Contains(".")) return null;
                string id = parts[0];
                string ver = parts.Length > 1 ? parts[1].Trim('"') : "";
                string name = parts.Length > 2 ? parts[2].Trim().Trim('"') : id;
                return new InstalledApp { BundleId = id, Version = ver, Name = string.IsNullOrEmpty(name) ? id : name };
            }
        }

        /// <summary>Instalira .ipa na uređaj.</summary>
        public static async Task InstallAppAsync(string udid, string ipaPath, CancellationToken ct)
        {
            string tool = ToolManager.Find("ideviceinstaller")
                ?? throw new InvalidOperationException("ideviceinstaller not found");
            if (string.IsNullOrEmpty(udid)) throw new InvalidOperationException("No device UDID.");
            var err = new StringBuilder();
            int code = await RunAsync(tool, new[] { "-u", udid, "install", ipaPath }, null, ct, null, err);
            if (code != 0)
                throw new InvalidOperationException(LastError(err.ToString()) ?? "App install failed.");
        }

        /// <summary>Deinstalira aplikaciju po bundle ID-u.</summary>
        public static async Task UninstallAppAsync(string udid, string bundleId, CancellationToken ct)
        {
            string tool = ToolManager.Find("ideviceinstaller")
                ?? throw new InvalidOperationException("ideviceinstaller not found");
            if (string.IsNullOrEmpty(udid)) throw new InvalidOperationException("No device UDID.");
            var err = new StringBuilder();
            int code = await RunAsync(tool, new[] { "-u", udid, "uninstall", bundleId }, null, ct, null, err);
            if (code != 0)
                throw new InvalidOperationException(LastError(err.ToString()) ?? "App uninstall failed.");
        }

        // ════════════════════════════════════════════════════════════════
        // LOGOVI / AKTIVACIJA
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Pokreće idevicesyslog i prosleđuje svaki red preko onLine dok se ct ne
        /// otkaže (Stop). RunAsync na otkazivanje ubije proces, pa je ovo i „stop".
        /// </summary>
        public static Task StartSyslogAsync(string udid, Action<string> onLine, CancellationToken ct)
        {
            string tool = ToolManager.Find("idevicesyslog")
                ?? throw new InvalidOperationException("idevicesyslog not found");
            if (string.IsNullOrEmpty(udid)) throw new InvalidOperationException("No device UDID.");
            return RunAsync(tool, new[] { "-u", udid }, onLine, ct);
        }

        /// <summary>Izvozi (prebacuje) crash logove sa uređaja u dati folder.</summary>
        public static async Task ExportCrashLogsAsync(string udid, string folder, CancellationToken ct)
        {
            string tool = ToolManager.Find("idevicecrashreport")
                ?? throw new InvalidOperationException("idevicecrashreport not found");
            if (string.IsNullOrEmpty(udid)) throw new InvalidOperationException("No device UDID.");
            var err = new StringBuilder();
            // "-e" prebaci (extract) izveštaje; "-k" bi ih zadržao na uređaju — mi prebacujemo.
            int code = await RunAsync(tool, new[] { "-u", udid, "-e", folder }, null, ct, null, err);
            if (code != 0)
                throw new InvalidOperationException(LastError(err.ToString()) ?? "Could not export crash logs.");
        }

        /// <summary>
        /// Pretvara .plist (uključujući binarni) u čitljiv XML pomoću plistutil-a.
        /// Ako plistutil ne postoji ili pretvaranje ne uspe, vraća null (pozivalac
        /// tada prikaže sirov sadržaj).
        /// </summary>
        public static async Task<string> ConvertPlistAsync(string path, CancellationToken ct)
        {
            string tool = ToolManager.Find("plistutil");
            if (tool == null) return null;
            var so = new StringBuilder();
            var se = new StringBuilder();
            int code = await RunAsync(tool, new[] { "-i", path }, null, ct, so, se);
            if (code != 0 || so.Length == 0) return null;
            return so.ToString();
        }

        /// <summary>Aktivira uređaj (posle restore-a). Vraća poruku alata radi prikaza.</summary>
        public static async Task<string> ActivateAsync(string udid, CancellationToken ct)
        {
            string tool = ToolManager.Find("ideviceactivation")
                ?? throw new InvalidOperationException("ideviceactivation not found");
            if (string.IsNullOrEmpty(udid)) throw new InvalidOperationException("No device UDID.");
            var so = new StringBuilder();
            var se = new StringBuilder();
            int code = await RunAsync(tool, new[] { "activate", "--udid", udid }, null, ct, so, se);
            string msg = (so.ToString() + "\n" + se.ToString()).Trim();
            if (code != 0)
                throw new InvalidOperationException(msg.Length > 0 ? msg : "Activation failed.");
            return msg.Length > 0 ? msg : "Activated.";
        }

        // ════════════════════════════════════════════════════════════════
        // SITNE RADNJE: restart, isključi, preimenuj, upari (Trust)
        // ════════════════════════════════════════════════════════════════

        /// <summary>Softverski restart uređaja (idevicediagnostics restart).</summary>
        public static async Task RestartAsync(string udid, CancellationToken ct)
        {
            string tool = ToolManager.Find("idevicediagnostics")
                ?? throw new InvalidOperationException("idevicediagnostics not found");
            if (string.IsNullOrEmpty(udid)) throw new InvalidOperationException("No device UDID.");
            var err = new StringBuilder();
            int code = await RunAsync(tool, new[] { "-u", udid, "restart" }, null, ct, null, err);
            if (code != 0)
                throw new InvalidOperationException(LastError(err.ToString()) ?? "Could not restart the device.");
        }

        /// <summary>Softversko isključivanje uređaja (idevicediagnostics shutdown).</summary>
        public static async Task ShutdownAsync(string udid, CancellationToken ct)
        {
            string tool = ToolManager.Find("idevicediagnostics")
                ?? throw new InvalidOperationException("idevicediagnostics not found");
            if (string.IsNullOrEmpty(udid)) throw new InvalidOperationException("No device UDID.");
            var err = new StringBuilder();
            int code = await RunAsync(tool, new[] { "-u", udid, "shutdown" }, null, ct, null, err);
            if (code != 0)
                throw new InvalidOperationException(LastError(err.ToString()) ?? "Could not shut down the device.");
        }

        /// <summary>Promena imena uređaja (idevicename -u UDID "Novo ime").</summary>
        public static async Task RenameAsync(string udid, string newName, CancellationToken ct)
        {
            string tool = ToolManager.Find("idevicename")
                ?? throw new InvalidOperationException("idevicename not found");
            if (string.IsNullOrEmpty(udid)) throw new InvalidOperationException("No device UDID.");
            if (string.IsNullOrWhiteSpace(newName)) throw new InvalidOperationException("Empty name.");
            var err = new StringBuilder();
            int code = await RunAsync(tool, new[] { "-u", udid, newName }, null, ct, null, err);
            if (code != 0)
                throw new InvalidOperationException(LastError(err.ToString()) ?? "Could not rename the device.");
        }

        /// <summary>
        /// Uparivanje (Trust). Korisnik na telefonu dodirne „Veruj / Trust". Vraća
        /// poruku alata (npr. uspeh ili zahtev za potvrdom na uređaju) radi prikaza.
        /// </summary>
        public static async Task<string> PairAsync(string udid, CancellationToken ct)
        {
            string tool = ToolManager.Find("idevicepair")
                ?? throw new InvalidOperationException("idevicepair not found");
            if (string.IsNullOrEmpty(udid)) throw new InvalidOperationException("No device UDID.");
            var so = new StringBuilder();
            var se = new StringBuilder();
            int code = await RunAsync(tool, new[] { "-u", udid, "pair" }, null, ct, so, se);
            string msg = (so.ToString() + "\n" + se.ToString()).Trim();
            if (string.IsNullOrEmpty(msg)) msg = code == 0 ? "Paired." : "Pairing failed.";
            return msg;
        }

        /// <summary>
        /// Uključuje/isključuje VoiceOver na telefonu preko lockdown domena
        /// com.apple.Accessibility (isto što je stari iTunes radio kroz „Configure
        /// Accessibility"). Koristi mali alat ideviceaccessibility. Uređaj mora biti
        /// uparen/„trusted" — na sasvim novom „Hello" ekranu bez uparivanja može da
        /// ne prođe (lockdown handshake padne), što alat i prijavi.
        /// </summary>
        public static async Task SetVoiceOverAsync(string udid, bool on, CancellationToken ct)
        {
            string tool = ToolManager.Find("ideviceaccessibility")
                ?? throw new InvalidOperationException("ideviceaccessibility not found");
            if (string.IsNullOrEmpty(udid)) throw new InvalidOperationException("No device UDID.");
            var err = new StringBuilder();
            int code = await RunAsync(tool, new[] { "-u", udid, on ? "on" : "off" }, null, ct, null, err);
            if (code != 0)
                throw new InvalidOperationException(LastError(err.ToString())
                    ?? "Could not change VoiceOver. The device may need Trust/pairing first.");
        }

        // ════════════════════════════════════════════════════════════════
        // BACKUP / RESTORE BACKUP  (idevicebackup2)
        // ════════════════════════════════════════════════════════════════
        public static async Task BackupAsync(string udid, string folder, Action<Progress> onProgress, CancellationToken ct)
        {
            string tool = ToolManager.Find("idevicebackup2")
                ?? throw new InvalidOperationException("idevicebackup2 not found");
            var prog = new Progress { Step = "Backup" };
            var err = new StringBuilder();
            // "backup --full <folder>" pravi pun backup u podfolder nazvan po UDID-u.
            int code = await RunAsync(tool, new[] { "-u", udid, "backup", "--full", folder },
                line => { if (ParseBackupLine(line, prog)) onProgress?.Invoke(prog); }, ct, null, err);
            if (code != 0)
                throw new InvalidOperationException(LastError(err.ToString()) ?? "Backup failed.");
        }

        public static async Task RestoreBackupAsync(string udid, string folder, Action<Progress> onProgress, CancellationToken ct)
        {
            string tool = ToolManager.Find("idevicebackup2")
                ?? throw new InvalidOperationException("idevicebackup2 not found");
            var prog = new Progress { Step = "Restore" };
            var err = new StringBuilder();
            int code = await RunAsync(tool, new[] { "-u", udid, "restore", "--system", "--settings", folder },
                line => { if (ParseBackupLine(line, prog)) onProgress?.Invoke(prog); }, ct, null, err);
            if (code != 0)
                throw new InvalidOperationException(LastError(err.ToString()) ?? "Restore from backup failed.");
        }

        public static bool ParseBackupLine(string line, Progress p)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            var m = Regex.Match(line, @"(\d{1,3}(?:\.\d+)?)\s*%");
            if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double pct))
            {
                pct = Math.Clamp(pct, 0, 100);
                if (Math.Abs(pct - p.Percent) >= 1) { p.Percent = pct; return true; }
            }
            return false;
        }

        // ════════════════════════════════════════════════════════════════
        // POMOĆNO
        // ════════════════════════════════════════════════════════════════
        /// <summary>Briše sav sadržaj firmver foldera (preuzeti IPSW + raspakovano), zadržava folder.</summary>
        public static void CleanFirmware(string dir)
        {
            try
            {
                if (!System.IO.Directory.Exists(dir)) return;
                foreach (var f in System.IO.Directory.GetFiles(dir))
                    try { System.IO.File.Delete(f); } catch { }
                foreach (var d in System.IO.Directory.GetDirectories(dir))
                    try { System.IO.Directory.Delete(d, true); } catch { }
            }
            catch { }
        }

        public static string LastError(string stderr)
        {
            var lines = (stderr ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            var errLine = lines.LastOrDefault(l => l.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
                          ?? lines.LastOrDefault();
            if (errLine == null) return null;
            errLine = Regex.Replace(errLine, @"^ERROR:\s*", "");
            return errLine.Length > 300 ? errLine[..300] + "…" : errLine;
        }

        /// <summary>
        /// Kao RunAsync, ali sa tajm-autom: ako alat ne završi u zadatom broju sekundi,
        /// proces se gasi i vraća se -1. Sprečava da neki alat (npr. irecovery -q kad
        /// nema uređaja) „visi" i drži svoj .exe zauzetim, pa se ne može prepisati pri
        /// instalaciji/ažuriranju alata.
        /// </summary>
        public static async Task<int> RunWithTimeoutAsync(string exe, string[] args, CancellationToken ct,
            int seconds, StringBuilder stdout = null, StringBuilder stderr = null)
        {
            using var to = CancellationTokenSource.CreateLinkedTokenSource(ct);
            to.CancelAfter(TimeSpan.FromSeconds(seconds));
            try { return await RunAsync(exe, args, null, to.Token, stdout, stderr); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return -1; }
        }

        /// <summary>
        /// Pokreće proces, čita stdout i stderr red po red. Otkazivanje gasi ceo
        /// proces. Isti obrazac kao DownloadEngine.RunAsync u ostalim Ultra app-ovima.
        /// </summary>
        public static async Task<int> RunAsync(string exe, IEnumerable<string> args, Action<string> onLine,
            CancellationToken ct, StringBuilder stdout = null, StringBuilder stderr = null, string workingDir = null)
        {
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            };
            if (!string.IsNullOrEmpty(workingDir)) psi.WorkingDirectory = workingDir;
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) => { if (e.Data == null) return; lock (p) stdout?.AppendLine(e.Data); LogSink?.Invoke(e.Data); onLine?.Invoke(e.Data); };
            p.ErrorDataReceived  += (_, e) => { if (e.Data == null) return; lock (p) stderr?.AppendLine(e.Data); LogSink?.Invoke(e.Data); onLine?.Invoke(e.Data); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            try
            {
                await p.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                try { p.Kill(entireProcessTree: true); } catch { }
                throw;
            }
            p.WaitForExit();
            return p.ExitCode;
        }
    }

    /// <summary>Greška restore/update procesa — nosi izlazni kod i pokušava da izvuče iTunes/idevicerestore kod greške.</summary>
    public sealed class RestoreException : Exception
    {
        public int ExitCode { get; }
        public int? AppleErrorCode { get; }

        public RestoreException(int exitCode, string stderr)
            : base(BuildMessage(exitCode, stderr, out int? apple))
        {
            ExitCode = exitCode;
            AppleErrorCode = apple;
        }

        // Postavlja se unutar BuildMessage preko out parametra; pomoćni trik da bismo
        // i poruku i kod izvukli u jednom prolazu.
        private static string BuildMessage(int exitCode, string stderr, out int? apple)
        {
            apple = null;
            string last = DeviceEngine.LastError(stderr);
            // Tipičan oblik: "... (errorcode -20)" ili "error code 4013" ili "(14)".
            var m = Regex.Match(stderr ?? "", @"(?:error\s*code|errorcode)\s*[:=]?\s*(-?\d{1,5})", RegexOptions.IgnoreCase);
            if (!m.Success) m = Regex.Match(last ?? "", @"\((-?\d{1,5})\)");
            if (m.Success && int.TryParse(m.Groups[1].Value, out int code)) apple = Math.Abs(code);
            return last ?? $"Process exited with code {exitCode}.";
        }
    }
}
