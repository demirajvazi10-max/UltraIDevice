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
                await RunAsync(idTool, new[] { "-l" }, null, ct, sb);
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
                int code = await RunAsync(rec, new[] { "-q" }, null, ct, sb, sb);
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

        private static async Task<string> InfoKeyAsync(string tool, string udid, string key, CancellationToken ct)
        {
            var sb = new StringBuilder();
            await RunAsync(tool, new[] { "-u", udid, "-k", key }, null, ct, sb);
            return sb.ToString().Trim();
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

            var args = new List<string>();
            args.Add(erase ? "--erase" : "--update");
            if (latestSigned) args.Add("--latest");

            // Pozicioni IPSW ide na kraj (idevicerestore ga zahteva ako nije --latest).
            if (!latestSigned)
            {
                if (string.IsNullOrWhiteSpace(ipswPath))
                    throw new InvalidOperationException("No IPSW file selected.");
                args.Add(ipswPath);
            }

            var prog = new Progress();
            var err = new StringBuilder();
            int code = await RunAsync(tool, args, line =>
            {
                if (ParseRestoreLine(line, prog)) onProgress?.Invoke(prog);
            }, ct, null, err);

            if (code != 0)
                throw new RestoreException(code, err.ToString());
        }

        /// <summary>Tumači red izlaza idevicerestore-a; true = napredak se promenio.</summary>
        public static bool ParseRestoreLine(string line, Progress p)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            bool changed = false;

            // Korak: red koji počinje poznatom radnjom.
            foreach (var kw in RestoreSteps)
            {
                if (line.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (p.Step != kw) { p.Step = kw; changed = true; }
                    break;
                }
            }

            // Procenat: bilo gde u redu "45%" ili "45.2%".
            var m = Regex.Match(line, @"(\d{1,3}(?:\.\d+)?)\s*%");
            if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double pct))
            {
                pct = Math.Clamp(pct, 0, 100);
                if (Math.Abs(pct - p.Percent) >= 1) { p.Percent = pct; changed = true; }
            }

            return changed;
        }

        // Ključne reči koraka koje idevicerestore ispisuje (grubo, radi najave čitaču).
        private static readonly string[] RestoreSteps =
        {
            "Downloading", "Extracting", "Requesting", "Checking", "Preparing",
            "Entering recovery", "Sending filesystem", "Sending", "Flashing",
            "Verifying", "Restoring", "Updating baseband", "Baseband", "Finishing", "Rebooting"
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
        /// Pokreće proces, čita stdout i stderr red po red. Otkazivanje gasi ceo
        /// proces. Isti obrazac kao DownloadEngine.RunAsync u ostalim Ultra app-ovima.
        /// </summary>
        public static async Task<int> RunAsync(string exe, IEnumerable<string> args, Action<string> onLine,
            CancellationToken ct, StringBuilder stdout = null, StringBuilder stderr = null)
        {
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            };
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
