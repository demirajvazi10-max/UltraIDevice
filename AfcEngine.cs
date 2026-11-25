using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace UltraIDevice
{
    /// <summary>
    /// Pristup fajlovima na uređaju preko AFC servisa (alat `afcclient` iz
    /// libimobiledevice). Vidljiva je medijska particija (DCIM/fotografije,
    /// Downloads, Books...) i sandbox aplikacija koje to dozvoljavaju — bez
    /// jailbreak-a NE vidi se ceo sistem, samo ono što Apple izlaže.
    ///
    /// Sve ide preko DeviceEngine.RunAsync (isti obrazac pokretanja procesa kao
    /// ostatak aplikacije), pa izlaz završava i u logu.
    /// </summary>
    public static class AfcEngine
    {
        public sealed class AfcEntry
        {
            public string Name = "";
            public bool? IsDir;      // null = nepoznato (preskočili smo proveru tipa)
            public long Size = -1;
        }

        public static string ToolPath => ToolManager.Find("afcclient");
        public static bool Available => ToolPath != null;

        // Iznad ovoliko stavki u fascikli preskačemo proveru tipa po stavci
        // (inače bi fascikla sa hiljadama fotografija bila spora).
        private const int TypeDetectCap = 200;

        private static async Task<(int code, string outp, string err)> RunAsync(
            string udid, CancellationToken ct, params string[] cmd)
        {
            string tool = ToolPath
                ?? throw new InvalidOperationException("afcclient nije pronađen. Instaliraj alate (Tools > Install device tools).");
            var args = new List<string> { "-u", udid };
            args.AddRange(cmd);
            var so = new StringBuilder();
            var se = new StringBuilder();
            int code = await DeviceEngine.RunAsync(tool, args, null, ct, so, se);
            return (code, so.ToString(), se.ToString());
        }

        /// <summary>Spaja fasciklu i ime u AFC putanju (uvek sa "/" razdvojnikom).</summary>
        public static string JoinPath(string dir, string name)
        {
            if (string.IsNullOrEmpty(dir) || dir == "/") return "/" + name.TrimStart('/');
            return dir.TrimEnd('/') + "/" + name.TrimStart('/');
        }

        /// <summary>Roditeljska fascikla date putanje ("/" ako smo već u korenu).</summary>
        public static string Parent(string path)
        {
            if (string.IsNullOrEmpty(path) || path == "/") return "/";
            string p = path.TrimEnd('/');
            int i = p.LastIndexOf('/');
            return i <= 0 ? "/" : p.Substring(0, i);
        }

        /// <summary>Lista sadržaj fascikle. Baca izuzetak ako putanja nije fascikla.</summary>
        public static async Task<List<AfcEntry>> ListAsync(string udid, string path, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(path)) path = "/";
            var (code, outp, err) = await RunAsync(udid, ct, "ls", path);

            // afcclient na nekim verzijama vrati != 0 i prazan izlaz kad putanja nije fascikla.
            if (code != 0 && string.IsNullOrWhiteSpace(outp))
                throw new Exception(string.IsNullOrWhiteSpace(err) ? "Ne mogu da otvorim fasciklu." : err.Trim());

            var names = outp.Split('\n')
                            .Select(s => s.Trim().Trim('\r'))
                            .Where(s => s.Length > 0 && s != "." && s != "..")
                            .ToList();

            var list = new List<AfcEntry>();
            bool detail = names.Count <= TypeDetectCap;

            foreach (var n in names)
            {
                var e = new AfcEntry { Name = n };
                if (detail)
                {
                    try
                    {
                        var (ic, io, _) = await RunAsync(udid, ct, "info", JoinPath(path, n));
                        if (ic == 0 && !string.IsNullOrEmpty(io))
                        {
                            if (io.IndexOf("S_IFDIR", StringComparison.OrdinalIgnoreCase) >= 0) e.IsDir = true;
                            else if (io.IndexOf("S_IFREG", StringComparison.OrdinalIgnoreCase) >= 0) e.IsDir = false;

                            var m = Regex.Match(io, @"st_size:\s*(\d+)");
                            if (m.Success && long.TryParse(m.Groups[1].Value, out long sz)) e.Size = sz;
                        }
                    }
                    catch { /* tip ostaje nepoznat — stavka se i dalje prikazuje */ }
                }
                list.Add(e);
            }

            // Fascikle prvo, pa po imenu.
            list.Sort((a, b) =>
            {
                int da = a.IsDir == true ? 0 : 1, db = b.IsDir == true ? 0 : 1;
                if (da != db) return da - db;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        public static async Task DownloadAsync(string udid, string remote, string local, CancellationToken ct = default)
        {
            var (code, _, err) = await RunAsync(udid, ct, "get", remote, local);
            if (code != 0) throw new Exception(string.IsNullOrWhiteSpace(err) ? "Preuzimanje nije uspelo." : err.Trim());
        }

        public static async Task UploadAsync(string udid, string local, string remote, CancellationToken ct = default)
        {
            var (code, _, err) = await RunAsync(udid, ct, "put", local, remote);
            if (code != 0) throw new Exception(string.IsNullOrWhiteSpace(err) ? "Slanje fajla nije uspelo." : err.Trim());
        }

        public static async Task DeleteAsync(string udid, string remote, CancellationToken ct = default)
        {
            var (code, _, err) = await RunAsync(udid, ct, "rm", remote);
            if (code != 0) throw new Exception(string.IsNullOrWhiteSpace(err) ? "Brisanje nije uspelo." : err.Trim());
        }

        public static async Task MkdirAsync(string udid, string remote, CancellationToken ct = default)
        {
            var (code, _, err) = await RunAsync(udid, ct, "mkdir", remote);
            if (code != 0) throw new Exception(string.IsNullOrWhiteSpace(err) ? "Pravljenje fascikle nije uspelo." : err.Trim());
        }

        /// <summary>Čitljiva veličina fajla za prikaz/čitač.</summary>
        public static string HumanSize(long bytes)
        {
            if (bytes < 0) return "";
            if (bytes < 1024) return bytes + " B";
            double kb = bytes / 1024.0;
            if (kb < 1024) return $"{kb:F0} KB";
            double mb = kb / 1024.0;
            if (mb < 1024) return $"{mb:F1} MB";
            return $"{mb / 1024.0:F2} GB";
        }
    }
}
