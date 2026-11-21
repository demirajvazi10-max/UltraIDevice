using System;
using System.Collections.Generic;

namespace UltraIDevice
{
    public enum DfuFamily
    {
        FaceId,   // iPhone 8 i noviji (uključujući sve Face ID modele) + SE 2/3
        IPhone7,  // iPhone 7 / 7 Plus
        Legacy    // iPhone 6s, SE (1. gen) i stariji — dugme Home
    }

    /// <summary>
    /// Jedan korak DFU/recovery vodiča. Ako je HoldSeconds > 0, aplikacija govori
    /// odbrojavanje ("...5, 4, 3, 2, 1") dok korisnik drži dugmad; ako je 0, korak je
    /// ručni (uradi radnju pa pređi na sledeći kad budeš spreman).
    /// </summary>
    public sealed class DfuStep
    {
        public string Text { get; init; } = "";
        public int HoldSeconds { get; init; }
    }

    /// <summary>
    /// Govorni vodič za DFU i recovery mod, po familiji uređaja, na engleskom i
    /// srpskom. Za slepog korisnika ovo je ključ cele aplikacije: DFU se ulazi
    /// tačnim tajmingom dugmadi, a ovde se taj tajming izgovara i odbrojava.
    /// </summary>
    public static class DfuGuide
    {
        /// <summary>Familija prema identifikatoru modela; podrazumevano Face ID (najčešće danas).</summary>
        public static DfuFamily FamilyFor(string productType)
        {
            if (string.IsNullOrEmpty(productType)) return DfuFamily.FaceId;

            // iPhone9,1 / iPhone9,3 = iPhone 7; iPhone9,2 / iPhone9,4 = iPhone 7 Plus
            if (productType.StartsWith("iPhone9,", StringComparison.OrdinalIgnoreCase))
                return DfuFamily.IPhone7;

            // iPhone8,x = iPhone 6s / 6s Plus / SE(1) -> Home dugme (Legacy)
            if (productType.StartsWith("iPhone8,", StringComparison.OrdinalIgnoreCase))
                return DfuFamily.Legacy;

            // Sve iPhone10,x i više = iPhone 8 i noviji -> Face ID postupak.
            // (iPad-ovi sa Home dugmetom bi bili Legacy, ali ih ovde ne razdvajamo
            //  posebno — korisnik uvek može ručno izabrati familiju u prozoru.)
            return DfuFamily.FaceId;
        }

        public static string FamilyName(DfuFamily f, string lang) => f switch
        {
            DfuFamily.FaceId => lang == "sr" ? "iPhone 8 i noviji (Face ID)" : "iPhone 8 and newer (Face ID)",
            DfuFamily.IPhone7 => "iPhone 7 / 7 Plus",
            DfuFamily.Legacy => lang == "sr" ? "iPhone 6s i stariji (dugme Home)" : "iPhone 6s and older (Home button)",
            _ => ""
        };

        public static List<DfuStep> GetDfuSteps(DfuFamily family, string lang)
        {
            bool sr = lang == "sr";
            return family switch
            {
                DfuFamily.FaceId => new()
                {
                    new() { Text = sr ? "Poveži telefon na računar USB kablom." : "Connect the phone to the computer with a USB cable." },
                    new() { Text = sr ? "Pritisni i brzo pusti dugme za pojačavanje zvuka." : "Press and quickly release Volume Up." },
                    new() { Text = sr ? "Pritisni i brzo pusti dugme za utišavanje zvuka." : "Press and quickly release Volume Down." },
                    new() { Text = sr ? "Pritisni i DRŽI bočno dugme. Drži dok ekran ne postane crn." : "Press and HOLD the Side button until the screen goes black.", HoldSeconds = 10 },
                    new() { Text = sr ? "Ne puštaj bočno dugme. Sada istovremeno pritisni i drži i dugme za utišavanje. Drži oba." : "Keep holding Side. Now also press and hold Volume Down. Hold both.", HoldSeconds = 5 },
                    new() { Text = sr ? "Pusti bočno dugme, ali NASTAVI da držiš dugme za utišavanje." : "Release the Side button but KEEP holding Volume Down.", HoldSeconds = 10 },
                    new() { Text = sr ? "Pusti dugme za utišavanje. Ako je ekran ostao crn, telefon je u DFU modu." : "Release Volume Down. If the screen stayed black, the phone is in DFU mode." },
                },
                DfuFamily.IPhone7 => new()
                {
                    new() { Text = sr ? "Poveži telefon na računar USB kablom." : "Connect the phone to the computer with a USB cable." },
                    new() { Text = sr ? "Pritisni i drži istovremeno bočno dugme i dugme za utišavanje." : "Press and hold the Side button and Volume Down together.", HoldSeconds = 8 },
                    new() { Text = sr ? "Pusti bočno dugme, ali NASTAVI da držiš dugme za utišavanje." : "Release the Side button but KEEP holding Volume Down.", HoldSeconds = 8 },
                    new() { Text = sr ? "Pusti dugme za utišavanje. Ako je ekran crn, telefon je u DFU modu." : "Release Volume Down. If the screen is black, the phone is in DFU mode." },
                },
                _ => new()
                {
                    new() { Text = sr ? "Poveži telefon na računar USB kablom." : "Connect the phone to the computer with a USB cable." },
                    new() { Text = sr ? "Pritisni i drži istovremeno dugme Home i gornje (ili bočno) dugme za uključivanje." : "Press and hold the Home button and the Top (or Side) button together.", HoldSeconds = 8 },
                    new() { Text = sr ? "Pusti dugme za uključivanje, ali NASTAVI da držiš Home." : "Release the power button but KEEP holding Home.", HoldSeconds = 8 },
                    new() { Text = sr ? "Pusti Home. Ako je ekran crn, telefon je u DFU modu." : "Release Home. If the screen is black, the phone is in DFU mode." },
                }
            };
        }

        public static List<DfuStep> GetRecoverySteps(DfuFamily family, string lang)
        {
            bool sr = lang == "sr";
            // Recovery je lakši od DFU-a — ekran pokazuje kabl/računar. Ali držimo
            // isti vođeni format radi doslednosti i za slepe korisnike.
            return family switch
            {
                DfuFamily.FaceId => new()
                {
                    new() { Text = sr ? "Poveži telefon na računar USB kablom." : "Connect the phone to the computer with a USB cable." },
                    new() { Text = sr ? "Pritisni i brzo pusti dugme za pojačavanje zvuka." : "Press and quickly release Volume Up." },
                    new() { Text = sr ? "Pritisni i brzo pusti dugme za utišavanje zvuka." : "Press and quickly release Volume Down." },
                    new() { Text = sr ? "Pritisni i drži bočno dugme dok se ne pojavi ekran za povezivanje sa računarom, pa pusti." : "Press and hold the Side button until the connect-to-computer screen appears, then release.", HoldSeconds = 12 },
                    new() { Text = sr ? "Telefon je u recovery modu kad se na ekranu vidi kabl ka računaru." : "The phone is in recovery mode when the screen shows a cable pointing to a computer." },
                },
                DfuFamily.IPhone7 => new()
                {
                    new() { Text = sr ? "Poveži telefon na računar USB kablom." : "Connect the phone to the computer with a USB cable." },
                    new() { Text = sr ? "Pritisni i drži dugme za utišavanje dok se ne pojavi ekran za povezivanje, pa pusti." : "Press and hold Volume Down until the connect screen appears, then release.", HoldSeconds = 12 },
                    new() { Text = sr ? "Telefon je u recovery modu kad se vidi kabl ka računaru." : "The phone is in recovery mode when you see the cable-to-computer screen." },
                },
                _ => new()
                {
                    new() { Text = sr ? "Poveži telefon na računar USB kablom." : "Connect the phone to the computer with a USB cable." },
                    new() { Text = sr ? "Pritisni i drži dugme Home dok se ne pojavi ekran za povezivanje, pa pusti." : "Press and hold the Home button until the connect screen appears, then release.", HoldSeconds = 12 },
                    new() { Text = sr ? "Telefon je u recovery modu kad se vidi kabl ka računaru." : "The phone is in recovery mode when you see the cable-to-computer screen." },
                }
            };
        }
    }
}
