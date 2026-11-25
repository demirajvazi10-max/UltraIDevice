using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace UltraIDevice
{
    /// <summary>
    /// Detaljna dijagnostika uređaja preko idevicediagnostics (+ ideviceinfo).
    /// Čita zdravlje baterije (ciklusi, kapacitet, procenat), skladište, hardver i
    /// mrežu, pa sve slaže u čitljiv izveštaj za servis.
    ///
    /// POTPUNO asinhrono (nema blokirajućih poziva), sa ukupnim tajm-autom, pa ne
    /// može da zakuca aplikaciju. Sirov ispis je ograničen po veličini, da ogroman
    /// dump ne zamrzne prikaz.
    ///
    /// Napomena: puna „genuine parts / Parts &amp; Service History" (kao u Apple Store-u)
    /// ide preko Apple GSX servera i nije pouzdano dostupna ovim alatima.
    /// </summary>
    public static class DiagnosticsEngine
    {
        private const int RawDumpLimit = 6000; // znakova sirovog ispisa u izveštaju

        public static async Task<string> BuildReportAsync(string udid, CancellationToken outerCt = default)
        {
            // Ukupni tajm-aut: i u najgorem slučaju izveštaj se vrati (ili javi grešku),
            // nikad ne visi.
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
            cts.CancelAfter(TimeSpan.FromSeconds(45));
            var ct = cts.Token;

            bool sr = LanguageManager.Current == "sr";
            string diag = ToolManager.Find("idevicediagnostics");
            string infoTool = ToolManager.Find("ideviceinfo");

            // Samo AppleSmartBattery ioregentry — u njemu je SVE za bateriju (ciklusi,
            // kapacitet, napon, temperatura, serijski). "diagnostics All" (ogroman i
            // spor) i "mobilegestalt" (na novom iOS-u vraća samo "Deprecated") su
            // izbačeni, jer su dijagnostiku činili sporom (do 40s) bez koristi.
            string battReg = diag != null ? await Capture(diag, new[] { "-u", udid, "ioregentry", "AppleSmartBattery" }, ct) : "";
            string all = battReg.Trim();

            // ── Parsiranje baterije iz sirovog ispisa ──
            int cycles = FindInt(all, "CycleCount");
            int design = FindInt(all, "DesignCapacity");
            // iOS "Maximum Capacity" (Settings → Battery) = NominalChargeCapacity /
            // DesignCapacity. Zato taj ključ prvi, pa AppleRawMaxCapacity kao rezerva.
            // (MaxCapacity/FullChargeCapacity u GasGauge su procenti = 100, pa ih ne koristimo.)
            int fullCharge = FirstInt(all, "NominalChargeCapacity", "AppleRawMaxCapacity");
            int temp = FindInt(all, "Temperature");
            int voltage = FindInt(all, "Voltage");
            string battSerial = FindStr(all, "Serial");
            string battMfg = FindStr(all, "ManufactureDate") ?? FindStr(all, "BatteryManufactureDate");

            int healthPct = -1;
            if (design > 0 && fullCharge > 0 && fullCharge <= design * 2)
                healthPct = (int)Math.Round(fullCharge * 100.0 / design);

            // ── Osnovni podaci preko ideviceinfo (sve asinhrono) ──
            async Task<string> I(string key, string domain = null) =>
                infoTool == null ? "" : await InfoAsync(infoTool, udid, key, domain, ct);

            string model = await I("ProductType");
            string friendly = DeviceInfo.ModelNames.TryGetValue(model, out var fn) ? fn : model;
            string iosVer = await I("ProductVersion");
            string build = await I("BuildVersion");
            string name = await I("DeviceName");
            string serial = await I("SerialNumber");
            string imei = await I("InternationalMobileEquipmentIdentity");
            string imei2 = await I("InternationalMobileEquipmentIdentity2");
            string region = await I("RegionInfo");
            string modelNo = await I("ModelNumber");
            string cpu = await I("CPUArchitecture");
            string wifi = await I("WiFiAddress");
            string bt = await I("BluetoothAddress");
            string phone = await I("PhoneNumber");
            string activation = await I("ActivationState");
            string battPct = await I("BatteryCurrentCapacity", "com.apple.mobile.battery");
            string charging = await I("BatteryIsCharging", "com.apple.mobile.battery");
            string diskTotal = await I("TotalDiskCapacity", "com.apple.disk_usage");
            string diskFree = await I("TotalDataAvailable", "com.apple.disk_usage");

            // ── Sastavi izveštaj ──
            var sb = new StringBuilder();
            void H(string s) { sb.AppendLine(); sb.AppendLine("== " + s + " =="); }
            void L(string label, string val) { if (!string.IsNullOrWhiteSpace(val)) sb.AppendLine(label + ": " + val); }

            H(sr ? "UREĐAJ" : "DEVICE");
            L("Model", $"{friendly} ({model})");
            L("iOS", iosVer + (string.IsNullOrEmpty(build) ? "" : $" ({build})"));
            L(sr ? "Ime" : "Name", name);
            L(sr ? "Serijski broj" : "Serial number", serial);
            L(sr ? "Model broj" : "Model number", modelNo);
            L("IMEI", imei);
            L("IMEI2", imei2);
            L(sr ? "Region" : "Region", region);
            L(sr ? "Arhitektura" : "CPU architecture", cpu);
            L(sr ? "Broj telefona" : "Phone number", phone);
            L("UDID", udid);

            H(sr ? "BATERIJA I ZDRAVLJE" : "BATTERY & HEALTH");
            if (healthPct >= 0) L(sr ? "Zdravlje baterije (procena)" : "Battery health (estimate)", healthPct + "%");
            if (cycles > 0) L(sr ? "Broj ciklusa" : "Cycle count", cycles.ToString());
            if (design > 0) L(sr ? "Fabrički kapacitet" : "Design capacity", design + " mAh");
            if (fullCharge > 0) L(sr ? "Pun kapacitet sada" : "Full charge capacity", fullCharge + " mAh");
            if (!string.IsNullOrWhiteSpace(battPct)) L(sr ? "Trenutno napunjeno" : "Current charge",
                battPct + "%" + (charging.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ? (sr ? ", puni se" : ", charging") : ""));
            if (voltage > 0) L(sr ? "Napon" : "Voltage", voltage + " mV");
            if (temp > 0) L(sr ? "Temperatura" : "Temperature", (temp / 100.0).ToString("F1", CultureInfo.InvariantCulture) + " °C");
            L(sr ? "Serijski baterije" : "Battery serial", battSerial);
            L(sr ? "Datum proizvodnje baterije" : "Battery manufacture date", battMfg);
            if (diag == null)
                sb.AppendLine(sr
                    ? "(Zdravlje baterije traži alat idevicediagnostics — reinstaliraj alate pa će i to biti tu.)"
                    : "(Battery health needs the idevicediagnostics tool — reinstall device tools to get it.)");

            H(sr ? "SKLADIŠTE" : "STORAGE");
            if (long.TryParse(diskTotal.Trim(), out long dt) && dt > 0)
            {
                long.TryParse(diskFree.Trim(), out long df);
                L(sr ? "Ukupno" : "Total", DeviceInfo.Gb(dt));
                L(sr ? "Slobodno" : "Free", DeviceInfo.Gb(df));
                L(sr ? "Zauzeto" : "Used", DeviceInfo.Gb(dt - df));
            }

            H(sr ? "MREŽA I AKTIVACIJA" : "NETWORK & ACTIVATION");
            L("Wi-Fi", wifi);
            L("Bluetooth", bt);
            L(sr ? "Aktivacija" : "Activation", activation);

            H(sr ? "NAPOMENA O DELOVIMA" : "NOTE ON PARTS");
            sb.AppendLine(sr
                ? "Puna detekcija zamenjenih/originalnih delova (Parts & Service History) ide preko Apple GSX servera i nije pouzdano dostupna ovim alatima. Gore su podaci koje telefon sam izloži."
                : "Full replaced/genuine parts detection (Parts & Service History) goes through Apple's GSX server and is not reliably available via these tools. Above is what the device itself exposes.");

            if (!string.IsNullOrEmpty(all))
            {
                H(sr ? "SIROV ISPIS (za doterivanje)" : "RAW OUTPUT (for tuning)");
                sb.AppendLine(all.Length > RawDumpLimit
                    ? all.Substring(0, RawDumpLimit) + (sr ? "\n...(skraćeno)" : "\n...(truncated)")
                    : all);
            }

            return sb.ToString().TrimStart();
        }

        private static readonly string[] MobileGestaltKeys =
        {
            "InternationalMobileEquipmentIdentity", "InternationalMobileEquipmentIdentity2",
            "RegionInfo", "DeviceColor", "DeviceEnclosureColor", "HardwareModel"
        };

        private static async Task<string> Capture(string exe, IEnumerable<string> args, CancellationToken ct)
        {
            var sb = new StringBuilder();
            try { await DeviceEngine.RunWithTimeoutAsync(exe, args.ToArray(), ct, 12, sb, sb); }
            catch { /* tajm-aut ili greška — vrati šta ima */ }
            return sb.ToString();
        }

        private static async Task<string> InfoAsync(string tool, string udid, string key, string domain, CancellationToken ct)
        {
            var args = new List<string> { "-u", udid };
            if (!string.IsNullOrEmpty(domain)) { args.Add("-q"); args.Add(domain); }
            args.Add("-k"); args.Add(key);
            var sb = new StringBuilder();
            try { await DeviceEngine.RunWithTimeoutAsync(tool, args.ToArray(), ct, 8, sb); }
            catch { }
            return sb.ToString().Trim();
        }

        private static int FindInt(string text, string key)
        {
            var m = Regex.Match(text ?? "", $@"<key>{Regex.Escape(key)}</key>\s*<integer>(-?\d+)</integer>", RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int v)) return v;
            m = Regex.Match(text ?? "", $@"\b{Regex.Escape(key)}\b\s*[:=]\s*(-?\d+)", RegexOptions.IgnoreCase);
            return m.Success && int.TryParse(m.Groups[1].Value, out int v2) ? v2 : -1;
        }

        private static int FirstInt(string text, params string[] keys)
        {
            foreach (var k in keys) { int v = FindInt(text, k); if (v > 0) return v; }
            return -1;
        }

        private static string FindStr(string text, string key)
        {
            var m = Regex.Match(text ?? "", $@"<key>{Regex.Escape(key)}</key>\s*<string>([^<]+)</string>", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }
    }
}
