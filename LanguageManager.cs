using System;
using System.Collections.Generic;

namespace UltraIDevice
{
    /// <summary>
    /// Dvojezični tekstovi (engleski / srpski-latinica). Isti princip kao u ostalim
    /// Ultra aplikacijama: kontrole imaju x:Name, a MainWindow.ApplyLanguage() im
    /// postavlja tekst preko L("kljuc"). Ovde se čuva samo rečnik i trenutni jezik.
    ///
    /// Namerno NE diramo imena kontrola odavde — MainWindow sam zove L() za svaku,
    /// pa je sve vezivanje imena na jednom mestu (lakše je držati usklađeno).
    /// </summary>
    public static class LanguageManager
    {
        public static string Current { get; private set; } = "en";

        /// <summary>Okida se kad se jezik promeni, da se UI osveži.</summary>
        public static event Action LanguageChanged;

        public static void SetLanguage(string lang)
        {
            lang = lang == "sr" ? "sr" : "en";
            if (lang == Current) return;
            Current = lang;
            LanguageChanged?.Invoke();
        }

        /// <summary>Prevod za ključ. Ako nema u trenutnom jeziku -> engleski -> sam ključ.</summary>
        public static string L(string key)
        {
            if (Current == "sr" && Sr.TryGetValue(key, out var sr)) return sr;
            if (En.TryGetValue(key, out var en)) return en;
            return key;
        }

        /// <summary>Prevod sa umetnutim vrednostima: L("step_of", 2, 7).</summary>
        public static string L(string key, params object[] args)
        {
            try { return string.Format(L(key), args); }
            catch { return L(key); }
        }

        // ════════════════════════════════════════════════════════════════
        private static readonly Dictionary<string, string> En = new()
        {
            // Menu
            ["menu_file"] = "_File",
            ["menu_exit"] = "E_xit",
            ["menu_view"] = "_View",
            ["menu_theme"] = "Theme",
            ["theme_dark"] = "_Dark (default)",
            ["theme_light"] = "_Light",
            ["theme_contrast"] = "_High contrast",
            ["menu_textsize"] = "Text size",
            ["text_larger"] = "_Larger text",
            ["text_smaller"] = "_Smaller text",
            ["text_reset"] = "_Reset text size",
            ["menu_language"] = "_Language",
            ["lang_en"] = "_English",
            ["lang_sr"] = "_Serbian (Srpski)",
            ["menu_tools"] = "_Tools",
            ["tools_install"] = "_Install / update device tools...",
            ["tools_check_driver"] = "_Check Apple driver",
            ["tools_open_logs"] = "_Open logs folder",
            ["tools_speak_progress"] = "_Speak progress out loud",
            ["menu_help"] = "_Help",
            ["help_shortcuts"] = "_Keyboard shortcuts",
            ["help_errorcodes"] = "_Error code explanations",
            ["help_support"] = "How to _support this project",
            ["help_about"] = "_About",

            // Main window
            ["app_title"] = "Ultra iDevice",
            ["app_subtitle"] = "Accessible iPhone and iPad restore, update and backup. Part of the Ultra family.",
            ["grp_device"] = "Device",
            ["lbl_device_status"] = "Connected device:",
            ["btn_refresh"] = "_Refresh",
            ["device_none"] = "No device detected. Connect an iPhone or iPad with a USB cable.",
            ["device_scanning"] = "Looking for a device...",

            ["grp_firmware"] = "Firmware (iOS) source",
            ["rb_latest"] = "Latest signed iOS (downloaded automatically)",
            ["rb_ipsw"] = "Use an IPSW file I already have",
            ["lbl_ipsw_path"] = "IPSW file:",
            ["btn_browse_ipsw"] = "_Browse...",
            ["ipsw_none"] = "No file selected",

            ["grp_actions"] = "Actions",
            ["btn_info"] = "Device _information",
            ["btn_update"] = "_Update iOS (keep data)",
            ["btn_restore"] = "_Restore iOS (ERASES everything)...",
            ["btn_backup"] = "_Back up this device...",
            ["btn_restore_backup"] = "Restore from a _backup...",
            ["btn_recovery"] = "_Enter recovery mode",
            ["btn_exit_recovery"] = "E_xit recovery / DFU mode",
            ["btn_dfu"] = "Guided _DFU mode...",
            ["btn_cancel"] = "_Cancel",

            ["progress_idle"] = "Ready.",

            // Status / running
            ["status_ready"] = "Ready.",
            ["status_no_tools"] = "Device tools are not installed yet. Use Tools, Install device tools.",
            ["running_update"] = "Updating iOS — do not disconnect the device...",
            ["running_restore"] = "Restoring iOS — do not disconnect the device...",
            ["running_backup"] = "Backing up the device — do not disconnect...",
            ["running_restore_backup"] = "Restoring from backup — do not disconnect...",
            ["done_update"] = "Update finished. The device will restart.",
            ["done_restore"] = "Restore finished. The device will restart and show the setup screen.",
            ["done_backup"] = "Backup finished.",
            ["done_restore_backup"] = "Restore from backup finished.",
            ["cancelled"] = "Cancelled.",
            ["step_of"] = "Step {0} of {1}: {2}",
            ["percent"] = "{0}, {1}%",

            // Dialogs
            ["confirm_update_title"] = "Update iOS?",
            ["confirm_update_msg"] = "This updates {0} to the latest iOS without erasing data. Keep the device connected the whole time. Continue?",
            ["confirm_restore_title"] = "Restore and ERASE the device?",
            ["confirm_restore_msg"] = "This will ERASE EVERYTHING on {0} and install a clean iOS. All photos, apps and settings on the device will be lost. Make sure you have a backup. Continue?",
            ["confirm_restore_backup_title"] = "Restore from backup?",
            ["confirm_restore_backup_msg"] = "This will replace the current contents of {0} with the selected backup. Continue?",
            ["no_device_title"] = "No device",
            ["no_device_msg"] = "No iPhone or iPad is connected. Connect one with a USB cable and press Refresh.",
            ["recovery_entered"] = "The device is now in recovery mode.",
            ["recovery_exited"] = "The device is leaving recovery / DFU mode and restarting.",
            ["pick_backup_folder"] = "Choose where to save the backup",
            ["pick_restore_folder"] = "Choose the backup folder to restore from",
            ["pick_ipsw"] = "Choose an IPSW firmware file",

            // Tools / driver
            ["tools_missing_title"] = "Device tools not found",
            ["tools_missing_msg"] = "Ultra iDevice uses a small set of open-source command-line tools (libimobiledevice and idevicerestore) to talk to the device. They were not found.\n\nUse Tools, Install device tools to set them up, or place the tool .exe files in:\n{0}",
            ["tools_installing"] = "Installing device tools...",
            ["tools_installed"] = "Device tools are installed and ready.",
            ["tools_install_failed"] = "Could not install the device tools automatically. You can place them manually in:\n{0}\n\nReason: {1}",
            ["driver_ok"] = "Apple device driver found. The app can talk to connected devices.",
            ["driver_missing_title"] = "Apple driver not found",
            ["driver_missing_msg"] = "Windows needs Apple's device driver to see an iPhone or iPad over USB. It comes with the free Apple Devices app from the Microsoft Store (or with iTunes).\n\nInstall Apple Devices, connect the device once, then come back here.",

            ["about_title"] = "About Ultra iDevice",
            ["shortcuts_title"] = "Keyboard shortcuts",
        };

        private static readonly Dictionary<string, string> Sr = new()
        {
            // Meni
            ["menu_file"] = "_Fajl",
            ["menu_exit"] = "I_zlaz",
            ["menu_view"] = "_Prikaz",
            ["menu_theme"] = "Tema",
            ["theme_dark"] = "_Tamna (podrazumevano)",
            ["theme_light"] = "_Svetla",
            ["theme_contrast"] = "_Visok kontrast",
            ["menu_textsize"] = "Veličina teksta",
            ["text_larger"] = "_Veći tekst",
            ["text_smaller"] = "_Manji tekst",
            ["text_reset"] = "_Vrati veličinu teksta",
            ["menu_language"] = "_Jezik",
            ["lang_en"] = "_Engleski (English)",
            ["lang_sr"] = "_Srpski",
            ["menu_tools"] = "_Alati",
            ["tools_install"] = "_Instaliraj / ažuriraj alate za uređaje...",
            ["tools_check_driver"] = "_Proveri Apple drajver",
            ["tools_open_logs"] = "_Otvori folder sa logovima",
            ["tools_speak_progress"] = "_Izgovaraj napredak naglas",
            ["menu_help"] = "P_omoć",
            ["help_shortcuts"] = "_Prečice na tastaturi",
            ["help_errorcodes"] = "Objašnjenja _kodova grešaka",
            ["help_support"] = "Kako da po_držiš projekat",
            ["help_about"] = "_O programu",

            // Glavni prozor
            ["app_title"] = "Ultra iDevice",
            ["app_subtitle"] = "Pristupačan restore, update i backup za iPhone i iPad. Deo Ultra porodice.",
            ["grp_device"] = "Uređaj",
            ["lbl_device_status"] = "Povezan uređaj:",
            ["btn_refresh"] = "_Osveži",
            ["device_none"] = "Nije pronađen uređaj. Poveži iPhone ili iPad USB kablom.",
            ["device_scanning"] = "Tražim uređaj...",

            ["grp_firmware"] = "Izvor firmvera (iOS)",
            ["rb_latest"] = "Najnoviji potpisani iOS (preuzima se automatski)",
            ["rb_ipsw"] = "Koristi IPSW fajl koji već imam",
            ["lbl_ipsw_path"] = "IPSW fajl:",
            ["btn_browse_ipsw"] = "_Izaberi...",
            ["ipsw_none"] = "Nije izabran fajl",

            ["grp_actions"] = "Radnje",
            ["btn_info"] = "_Informacije o uređaju",
            ["btn_update"] = "_Ažuriraj iOS (čuva podatke)",
            ["btn_restore"] = "_Restore iOS-a (BRIŠE sve)...",
            ["btn_backup"] = "_Napravi backup ovog uređaja...",
            ["btn_restore_backup"] = "Vrati iz _backup-a...",
            ["btn_recovery"] = "_Uđi u recovery mod",
            ["btn_exit_recovery"] = "I_zađi iz recovery / DFU moda",
            ["btn_dfu"] = "Vođeni _DFU mod...",
            ["btn_cancel"] = "_Otkaži",

            ["progress_idle"] = "Spremno.",

            // Status / tok
            ["status_ready"] = "Spremno.",
            ["status_no_tools"] = "Alati za uređaje još nisu instalirani. Idi na Alati, Instaliraj alate.",
            ["running_update"] = "Ažuriram iOS — ne isključuj uređaj...",
            ["running_restore"] = "Radim restore iOS-a — ne isključuj uređaj...",
            ["running_backup"] = "Pravim backup uređaja — ne isključuj...",
            ["running_restore_backup"] = "Vraćam iz backup-a — ne isključuj...",
            ["done_update"] = "Ažuriranje završeno. Uređaj će se restartovati.",
            ["done_restore"] = "Restore završen. Uređaj će se restartovati i prikazati ekran za podešavanje.",
            ["done_backup"] = "Backup završen.",
            ["done_restore_backup"] = "Vraćanje iz backup-a završeno.",
            ["cancelled"] = "Otkazano.",
            ["step_of"] = "Korak {0} od {1}: {2}",
            ["percent"] = "{0}, {1}%",

            // Dijalozi
            ["confirm_update_title"] = "Ažurirati iOS?",
            ["confirm_update_msg"] = "Ovo ažurira {0} na najnoviji iOS bez brisanja podataka. Drži uređaj povezan sve vreme. Nastaviti?",
            ["confirm_restore_title"] = "Uraditi restore i OBRISATI uređaj?",
            ["confirm_restore_msg"] = "Ovo će OBRISATI SVE na {0} i instalirati čist iOS. Sve slike, aplikacije i podešavanja na uređaju biće izgubljeni. Proveri da imaš backup. Nastaviti?",
            ["confirm_restore_backup_title"] = "Vratiti iz backup-a?",
            ["confirm_restore_backup_msg"] = "Ovo će zameniti trenutni sadržaj uređaja {0} izabranim backup-om. Nastaviti?",
            ["no_device_title"] = "Nema uređaja",
            ["no_device_msg"] = "Nije povezan nijedan iPhone ni iPad. Poveži ga USB kablom pa pritisni Osveži.",
            ["recovery_entered"] = "Uređaj je sada u recovery modu.",
            ["recovery_exited"] = "Uređaj izlazi iz recovery / DFU moda i restartuje se.",
            ["pick_backup_folder"] = "Izaberi gde da se sačuva backup",
            ["pick_restore_folder"] = "Izaberi folder sa backup-om za vraćanje",
            ["pick_ipsw"] = "Izaberi IPSW firmver fajl",

            // Alati / drajver
            ["tools_missing_title"] = "Alati za uređaje nisu pronađeni",
            ["tools_missing_msg"] = "Ultra iDevice koristi mali skup open-source alata iz komandne linije (libimobiledevice i idevicerestore) za komunikaciju sa uređajem. Oni nisu pronađeni.\n\nIdi na Alati, Instaliraj alate, ili ručno stavi .exe fajlove alata u:\n{0}",
            ["tools_installing"] = "Instaliram alate za uređaje...",
            ["tools_installed"] = "Alati za uređaje su instalirani i spremni.",
            ["tools_install_failed"] = "Nije uspelo automatsko instaliranje alata. Možeš ih ručno staviti u:\n{0}\n\nRazlog: {1}",
            ["driver_ok"] = "Apple drajver je pronađen. Aplikacija može da komunicira sa povezanim uređajima.",
            ["driver_missing_title"] = "Apple drajver nije pronađen",
            ["driver_missing_msg"] = "Windows-u treba Apple-ov drajver da bi video iPhone ili iPad preko USB-a. Dolazi uz besplatnu aplikaciju Apple Devices sa Microsoft Store-a (ili uz iTunes).\n\nInstaliraj Apple Devices, poveži uređaj jednom, pa se vrati ovde.",

            ["about_title"] = "O programu Ultra iDevice",
            ["shortcuts_title"] = "Prečice na tastaturi",
        };
    }
}
