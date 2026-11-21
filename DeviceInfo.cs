using System.Collections.Generic;

namespace UltraIDevice
{
    public enum DeviceMode
    {
        None,       // ništa nije povezano
        Normal,     // normalan rad (iOS se pokrenuo)
        Recovery,   // recovery mod
        Dfu         // DFU mod
    }

    /// <summary>
    /// Sve što znamo o trenutno povezanom uređaju. Popunjava DeviceEngine.
    /// </summary>
    public sealed class DeviceInfo
    {
        public string Udid { get; set; } = "";
        public string ProductType { get; set; } = "";     // npr. "iPhone17,2"
        public string ProductVersion { get; set; } = "";  // iOS verzija, npr. "18.1"
        public string SerialNumber { get; set; } = "";
        public string DeviceName { get; set; } = "";       // ime koje je korisnik dao ("Demirov iPhone")
        public DeviceMode Mode { get; set; } = DeviceMode.None;

        /// <summary>Lepo ime modela ("iPhone 16 Pro Max") iz ProductType identifikatora.</summary>
        public string FriendlyModel => ModelNames.TryGetValue(ProductType, out var n) ? n : ProductType;

        /// <summary>Jedna rečenica za čitač ekrana i za prikaz na ekranu.</summary>
        public string Describe(string lang)
        {
            if (Mode == DeviceMode.None)
                return LanguageManager.L("device_none");

            string model = string.IsNullOrEmpty(FriendlyModel) ? "Apple device" : FriendlyModel;
            string modeText = Mode switch
            {
                DeviceMode.Recovery => lang == "sr" ? "recovery mod" : "recovery mode",
                DeviceMode.Dfu => "DFU",
                _ => lang == "sr" ? "normalan rad" : "normal mode"
            };

            if (Mode == DeviceMode.Normal)
            {
                string ios = string.IsNullOrEmpty(ProductVersion) ? "" : (lang == "sr" ? $", iOS {ProductVersion}" : $", iOS {ProductVersion}");
                string name = string.IsNullOrEmpty(DeviceName) ? "" : $" — {DeviceName}";
                return $"{model}{ios}, {modeText}{name}";
            }

            // U recovery/DFU modu iOS verzija nije dostupna
            return $"{model}, {modeText}";
        }

        /// <summary>
        /// Lepa imena modela za novije iPhone/iPad identifikatore. Nije potpuna lista
        /// (Apple stalno dodaje nove), ali pokriva uređaje koji se danas servisiraju.
        /// Za nepoznat identifikator prikazuje se sam identifikator (npr. "iPhone18,3").
        /// </summary>
        public static readonly Dictionary<string, string> ModelNames = new()
        {
            // iPhone 16 (2024)
            ["iPhone17,3"] = "iPhone 16",
            ["iPhone17,4"] = "iPhone 16 Plus",
            ["iPhone17,1"] = "iPhone 16 Pro",
            ["iPhone17,2"] = "iPhone 16 Pro Max",
            ["iPhone17,5"] = "iPhone 16e",
            // iPhone 15 (2023)
            ["iPhone15,4"] = "iPhone 15",
            ["iPhone15,5"] = "iPhone 15 Plus",
            ["iPhone16,1"] = "iPhone 15 Pro",
            ["iPhone16,2"] = "iPhone 15 Pro Max",
            // iPhone 14 (2022)
            ["iPhone14,7"] = "iPhone 14",
            ["iPhone14,8"] = "iPhone 14 Plus",
            ["iPhone15,2"] = "iPhone 14 Pro",
            ["iPhone15,3"] = "iPhone 14 Pro Max",
            // iPhone 13 (2021)
            ["iPhone14,5"] = "iPhone 13",
            ["iPhone14,4"] = "iPhone 13 mini",
            ["iPhone14,2"] = "iPhone 13 Pro",
            ["iPhone14,3"] = "iPhone 13 Pro Max",
            // iPhone 12 (2020)
            ["iPhone13,2"] = "iPhone 12",
            ["iPhone13,1"] = "iPhone 12 mini",
            ["iPhone13,3"] = "iPhone 12 Pro",
            ["iPhone13,4"] = "iPhone 12 Pro Max",
            // iPhone 11 (2019) + SE
            ["iPhone12,1"] = "iPhone 11",
            ["iPhone12,3"] = "iPhone 11 Pro",
            ["iPhone12,5"] = "iPhone 11 Pro Max",
            ["iPhone12,8"] = "iPhone SE (2nd gen)",
            ["iPhone14,6"] = "iPhone SE (3rd gen)",
            // iPhone X / 8
            ["iPhone10,3"] = "iPhone X",
            ["iPhone10,6"] = "iPhone X",
            ["iPhone11,2"] = "iPhone XS",
            ["iPhone11,4"] = "iPhone XS Max",
            ["iPhone11,6"] = "iPhone XS Max",
            ["iPhone11,8"] = "iPhone XR",
            ["iPhone10,1"] = "iPhone 8",
            ["iPhone10,4"] = "iPhone 8",
            ["iPhone10,2"] = "iPhone 8 Plus",
            ["iPhone10,5"] = "iPhone 8 Plus",
        };
    }
}
