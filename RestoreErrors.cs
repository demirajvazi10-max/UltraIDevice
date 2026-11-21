using System.Collections.Generic;
using System.Linq;

namespace UltraIDevice
{
    /// <summary>
    /// Pretvara brojne kodove grešaka (iTunes / idevicerestore) u jasno objašnjenje
    /// i predlog šta da se uradi — na engleskom i srpskom. Ovo je ono što 3uTools i
    /// Apple Devices ne rade: oni izbace goli broj, a korisnik ne zna šta znači.
    ///
    /// Lista nije potpuna (Apple ima mnogo kodova), ali pokriva one koji se u praksi
    /// najčešće vide pri restore/update-u. Za nepoznat kod daje se opšti savet.
    /// </summary>
    public static class RestoreErrors
    {
        public sealed class Entry
        {
            public int Code;
            public string En;
            public string Sr;
        }

        /// <summary>Objašnjenje za kod, na trenutnom jeziku. Nikad null.</summary>
        public static string Explain(int? code, string lang)
        {
            if (code.HasValue)
            {
                var e = All.FirstOrDefault(x => x.Code == code.Value);
                if (e != null) return lang == "sr" ? e.Sr : e.En;
                return lang == "sr"
                    ? $"Kod greške {code.Value}. Nepoznat kod. Probaj drugi USB kabl i port (direktno u računar, bez razdelnika/huba), isključi antivirus privremeno, i proveri da je uređaj dovoljno napunjen."
                    : $"Error code {code.Value}. Unknown code. Try a different USB cable and port (straight into the computer, no hub), temporarily disable antivirus, and make sure the device is charged.";
            }
            return lang == "sr"
                ? "Nepoznata greška, bez koda. Probaj ponovo sa drugim kablom i USB portom, pa ako se ponovi, pošalji mi tačan tekst greške."
                : "Unknown error, no code. Try again with a different cable and USB port; if it repeats, send me the exact error text.";
        }

        /// <summary>Cela lista, za dijalog Pomoć, Objašnjenja kodova grešaka.</summary>
        public static readonly List<Entry> All = new()
        {
            new() { Code = 1,
                En = "Error 1: Usually a hardware or baseband issue, or trying to install an iOS version Apple no longer signs. Try the latest signed iOS.",
                Sr = "Greška 1: Obično hardverski ili baseband problem, ili pokušaj instalacije iOS verzije koju Apple više ne potpisuje. Probaj najnoviji potpisani iOS." },
            new() { Code = 2,
                En = "Error 2: The firmware could not be built. Re-download the IPSW or use the Latest signed option.",
                Sr = "Greška 2: Firmver nije mogao da se pripremi. Ponovo preuzmi IPSW ili koristi opciju Najnoviji potpisani iOS." },
            new() { Code = 6,
                En = "Error 6: Communication problem during restore — most often the USB cable or port. Use an Apple-quality cable straight into a rear USB port.",
                Sr = "Greška 6: Problem u komunikaciji tokom restore-a — najčešće USB kabl ili port. Koristi kvalitetan (Apple) kabl direktno u zadnji USB port." },
            new() { Code = 9,
                En = "Error 9: The device disconnected during restore — the USB connection dropped. Change the cable and port, close other USB-heavy apps, and keep the device still during the restore.",
                Sr = "Greška 9: Uređaj se otkačio tokom restore-a — pukla je USB veza. Promeni kabl i port, zatvori druge aplikacije koje koriste USB, i ne pomeraj uređaj tokom restore-a." },
            new() { Code = 14,
                En = "Error 14: USB data problem while transferring the firmware. Try another cable and a different (preferably USB-2) port, and disable security software temporarily.",
                Sr = "Greška 14: Problem sa USB prenosom podataka pri prebacivanju firmvera. Probaj drugi kabl i drugi port (po mogućstvu USB-2), i privremeno isključi zaštitni softver." },
            new() { Code = 10,
                En = "Error 10: Low-level firmware (LLB) failed to load. Usually fixed by re-trying with the latest signed iOS.",
                Sr = "Greška 10: Nizak nivo firmvera (LLB) se nije učitao. Obično se reši ponovnim pokušajem sa najnovijim potpisanim iOS-om." },
            new() { Code = 4005,
                En = "Error 4005: USB connection broke during restore, often in DFU mode. Change the cable/port and redo the DFU steps carefully.",
                Sr = "Greška 4005: USB veza je pukla tokom restore-a, često u DFU modu. Promeni kabl/port i pažljivo ponovi DFU korake." },
            new() { Code = 4013,
                En = "Error 4013: Communication error during restore — most commonly the cable, the USB port, or a USB hub. Use a short, good cable directly into the computer, and try a different port.",
                Sr = "Greška 4013: Greška u komunikaciji tokom restore-a — najčešće kabl, USB port ili USB razdelnik (hub). Koristi kratak, dobar kabl direktno u računar, i probaj drugi port." },
            new() { Code = 4014,
                En = "Error 4014: The device disconnected from USB during restore. Same fixes as 4013: different cable, direct port; sometimes a hardware (storage) fault on the device.",
                Sr = "Greška 4014: Uređaj se otkačio sa USB-a tokom restore-a. Isti lekovi kao 4013: drugi kabl, direktan port; ponekad je kvar hardvera (memorije) na uređaju." },
            new() { Code = 3194,
                En = "Error 3194: Apple is no longer signing that iOS version (or a hosts-file block). Use the Latest signed iOS — you cannot restore to an unsigned version.",
                Sr = "Greška 3194: Apple više ne potpisuje tu iOS verziju (ili blokada u hosts fajlu). Koristi Najnoviji potpisani iOS — na nepotpisanu verziju se ne može vratiti." },
            new() { Code = 3004,
                En = "Error 3004: Could not reach Apple's servers. Check the internet connection and try again.",
                Sr = "Greška 3004: Nije moguće dohvatiti Apple servere. Proveri internet vezu i probaj ponovo." },
            new() { Code = 40,
                En = "Error 40: Hardware/security problem reported by the device during restore. Redo DFU carefully; if it persists it may be a hardware fault.",
                Sr = "Greška 40: Uređaj je prijavio hardverski/bezbednosni problem tokom restore-a. Pažljivo ponovi DFU; ako se ponavlja, može biti kvar hardvera." },
            new() { Code = 20,
                En = "Error 20: The device is in the wrong mode for the requested step. Exit recovery/DFU and start over, or redo DFU mode.",
                Sr = "Greška 20: Uređaj je u pogrešnom modu za traženi korak. Izađi iz recovery/DFU i počni iznova, ili ponovi DFU mod." },
            new() { Code = 21,
                En = "Error 21: Could not get the device into the needed mode (often a DFU timing problem, or a cable issue). Redo the DFU steps.",
                Sr = "Greška 21: Nije uspelo dovođenje uređaja u potrebni mod (često problem sa DFU tajmingom ili kablom). Ponovi DFU korake." },
            new() { Code = 2009,
                En = "Error 2009: USB problem, often a faulty or dirty port/cable. Clean the device's charging port, change the cable, and try another USB port.",
                Sr = "Greška 2009: USB problem, često neispravan ili prljav port/kabl. Očisti konektor za punjenje na uređaju, promeni kabl i probaj drugi USB port." },
            new() { Code = 53,
                En = "Error 53: A hardware check failed (historically Touch ID / sensor). Make sure original parts are connected; may be a hardware fault.",
                Sr = "Greška 53: Provera hardvera nije prošla (istorijski Touch ID / senzor). Proveri da su originalni delovi povezani; može biti kvar hardvera." },
        };
    }
}
