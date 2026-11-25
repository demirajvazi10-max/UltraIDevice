using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WinForms = System.Windows.Forms;

namespace UltraIDevice
{
    public partial class MainWindow : Window
    {
        private AppSettings _settings;
        private DeviceInfo _device = new() { Mode = DeviceMode.None };
        private CancellationTokenSource _cts;
        private bool _busy;
        private double _lastSpokenPct = -100;
        private string _lastSpokenStep = "";

        public MainWindow()
        {
            InitializeComponent();
            UiScaling.Register(this);

            // Sav sirov izlaz alata ide u log — da kasnije doteramo parsiranje.
            DeviceEngine.LogSink = Logger.Write;

            _settings = AppSettings.Load();
            DeviceEngine.KeepFirmware = _settings.KeepFirmware;  // podrazumevano briše firmver posle instalacije

            // Tema: ako korisnik nije birao, a čitač ekrana radi -> visok kontrast.
            string theme = _settings.Theme;
            if (string.IsNullOrEmpty(theme))
                theme = ScreenReaderDetector.IsScreenReaderActive() ? "contrast" : "dark";
            ApplyTheme(theme, save: false);

            // Jezik
            LanguageManager.SetLanguage(string.IsNullOrEmpty(_settings.Language) ? "en" : _settings.Language);
            LanguageManager.LanguageChanged += ApplyLanguage;

            mnuSpeakProgress.IsChecked = _settings.SpeakProgress;

            ApplyLanguage();
            txbAppStatus.Text = $"Ultra iDevice {GetAppVersion()}";

            Loaded += async (s, e) => await InitialCheckAsync();
        }

        private static string GetAppVersion()
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return v == null ? "1.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }

        // ════════════════════════════════════════════════════════════════
        // JEZIK / TEMA / VELIČINA
        // ════════════════════════════════════════════════════════════════
        private void ApplyLanguage()
        {
            string L(string k) => LanguageManager.L(k);

            // Meni
            mnuFileRoot.Header = L("menu_file");
            mnuExit.Header = L("menu_exit");
            mnuViewRoot.Header = L("menu_view");
            mnuThemeRoot.Header = L("menu_theme");
            mnuThemeDark.Header = L("theme_dark");
            mnuThemeLight.Header = L("theme_light");
            mnuThemeContrast.Header = L("theme_contrast");
            mnuTextSizeRoot.Header = L("menu_textsize");
            mnuLarger.Header = L("text_larger");
            mnuSmaller.Header = L("text_smaller");
            mnuReset.Header = L("text_reset");
            mnuLanguageRoot.Header = L("menu_language");
            mnuLangEn.Header = L("lang_en");
            mnuLangSr.Header = L("lang_sr");
            mnuToolsRoot.Header = L("menu_tools");
            mnuInstallTools.Header = L("tools_install");
            mnuCheckDriver.Header = L("tools_check_driver");
            mnuOpenLogs.Header = L("tools_open_logs");
            mnuSpeakProgress.Header = L("tools_speak_progress");
            mnuHelpRoot.Header = L("menu_help");
            mnuShortcuts.Header = L("help_shortcuts");
            mnuErrorCodes.Header = L("help_errorcodes");
            mnuSupport.Header = L("help_support");
            mnuAbout.Header = L("help_about");

            // Glavni prozor
            txbAppTitle.Text = L("app_title");
            txbAppSubtitle.Text = L("app_subtitle");
            grpDevice.Header = L("grp_device");
            lblDeviceStatus.Content = L("lbl_device_status");
            btnRefresh.Content = L("btn_refresh");
            grpFirmware.Header = L("grp_firmware");
            rbLatest.Content = L("rb_latest");
            rbIpsw.Content = L("rb_ipsw");
            lblIpswPath.Content = L("lbl_ipsw_path");
            btnBrowseIpsw.Content = L("btn_browse_ipsw");
            grpActions.Header = L("grp_actions");
            btnInfo.Content = L("btn_info");
            btnDiagnostics.Content = L("btn_diagnostics");
            btnUpdate.Content = L("btn_update");
            btnRestore.Content = L("btn_restore");
            btnBackup.Content = L("btn_backup");
            btnRestoreBackup.Content = L("btn_restore_backup");
            btnRecovery.Content = L("btn_recovery");
            btnExitRecovery.Content = L("btn_exit_recovery");
            btnDfu.Content = L("btn_dfu");
            btnFiles.Content = L("btn_files");
            btnTrust.Content = L("btn_trust");
            btnRename.Content = L("btn_rename");
            btnRestart.Content = L("btn_restart");
            btnShutdown.Content = L("btn_shutdown");
            btnVoiceOverOn.Content = L("btn_vo_on");
            btnVoiceOverOff.Content = L("btn_vo_off");
            btnApps.Content = L("btn_apps");
            btnSyslog.Content = L("btn_syslog");
            btnCrash.Content = L("btn_crash");
            btnActivate.Content = L("btn_activate");
            btnCancel.Content = L("btn_cancel");

            if (string.IsNullOrEmpty(txtIpswPath.Text)) txtIpswPath.Text = "";
            if (string.IsNullOrEmpty(txbProgress.Text)) txbProgress.Text = L("progress_idle");

            // Osveži opis uređaja na novom jeziku
            txbDeviceStatus.Text = _device.Describe(LanguageManager.Current);

            UpdateMenuChecks();
        }

        private void UpdateMenuChecks()
        {
            string theme = _settings.Theme;
            if (string.IsNullOrEmpty(theme)) theme = "dark";
            mnuThemeDark.IsChecked = theme == "dark";
            mnuThemeLight.IsChecked = theme == "light";
            mnuThemeContrast.IsChecked = theme == "contrast";
            mnuLangEn.IsChecked = LanguageManager.Current == "en";
            mnuLangSr.IsChecked = LanguageManager.Current == "sr";
        }

        private void ApplyTheme(string name, bool save = true)
        {
            string file = name switch
            {
                "light" => "Themes/Theme.Light.xaml",
                "contrast" => "Themes/Theme.Contrast.xaml",
                _ => "Themes/Theme.Dark.xaml"
            };
            var dict = new ResourceDictionary { Source = new Uri(file, UriKind.Relative) };
            var merged = System.Windows.Application.Current.Resources.MergedDictionaries;
            if (merged.Count > 0) merged[0] = dict; else merged.Add(dict);

            _settings.Theme = name;
            if (save) _settings.Save();
            UpdateMenuChecks();
        }

        private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is string tag) ApplyTheme(tag);
        }

        private void LanguageMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is string tag)
            {
                LanguageManager.SetLanguage(tag);
                _settings.Language = tag;
                _settings.Save();
            }
        }

        private void LargerText_Click(object sender, RoutedEventArgs e) => UiScaling.SetScale(UiScaling.CurrentScale + 0.1);
        private void SmallerText_Click(object sender, RoutedEventArgs e) => UiScaling.SetScale(UiScaling.CurrentScale - 0.1);
        private void ResetText_Click(object sender, RoutedEventArgs e) => UiScaling.SetScale(1.0);

        // ════════════════════════════════════════════════════════════════
        // POČETNA PROVERA + OSVEŽAVANJE
        // ════════════════════════════════════════════════════════════════
        private async Task InitialCheckAsync()
        {
            var missing = ToolManager.MissingTools();
            if (missing.Count == ToolManager.CoreTools.Length)
            {
                // Nijedan alat nije prisutan.
                SetProgress(LanguageManager.L("status_no_tools"), speak: true);
            }
            else if (missing.Count > 0)
            {
                // Neki rade, neki fale — reci koji fale, ali ne blokiraj ostalo.
                bool sr = LanguageManager.Current == "sr";
                SetProgress((sr ? "Spremno. Nedostaje: " : "Ready. Missing: ") + string.Join(", ", missing)
                    + (sr ? " (potrebno za update/restore)." : " (needed for update/restore)."), speak: true);
            }
            await RefreshDeviceAsync();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await RefreshDeviceAsync();

        private async Task RefreshDeviceAsync()
        {
            if (_busy) return;
            Announcer.Say(txbDeviceStatus, LanguageManager.L("device_scanning"));
            try
            {
                _device = await Task.Run(() => DeviceEngine.DetectAsync());
            }
            catch
            {
                _device = new DeviceInfo { Mode = DeviceMode.None };
            }
            Announcer.Say(txbDeviceStatus, _device.Describe(LanguageManager.Current));
            UpdateActionStates();
        }

        /// <summary>
        /// Posle uspešnog restore/update-a telefon se ponovo pokreće. Ne pokušavamo
        /// odmah detekciju (lockdownd još nije spreman — otud ranije "Mux error (-8)").
        /// Umesto toga javljamo da se uređaj diže i tiho proveravamo dok se ne vrati
        /// (dok ne pročitamo iOS verziju), pa tek onda prikažemo uređaj.
        /// </summary>
        private async Task RefreshAfterRestoreAsync()
        {
            SetProgress(LanguageManager.L("rebooting_wait"), speak: true);
            Announcer.Say(txbDeviceStatus, LanguageManager.L("rebooting_wait"));
            UpdateActionStates();

            // ~2 minuta: 24 pokušaja na svakih 5s. Uređaj je stvarno nazad tek kad
            // pročitamo iOS verziju (znak da je lockdownd podignut).
            for (int i = 0; i < 24; i++)
            {
                await Task.Delay(5000);
                DeviceInfo d;
                try { d = await Task.Run(() => DeviceEngine.DetectAsync()); }
                catch { d = new DeviceInfo { Mode = DeviceMode.None }; }

                if (d.Mode == DeviceMode.Normal && !string.IsNullOrEmpty(d.ProductVersion))
                {
                    _device = d;
                    Announcer.Say(txbDeviceStatus, _device.Describe(LanguageManager.Current));
                    SetProgress(LanguageManager.L("status_ready"), speak: true);
                    UpdateActionStates();
                    return;
                }
            }

            // Isteklo: obično osveži (korisnik uvek može i ručno Refresh / F5).
            await RefreshDeviceAsync();
        }

        private void UpdateActionStates()
        {
            bool idle = !_busy;
            var mode = _device.Mode;
            bool anyDevice = mode != DeviceMode.None;
            bool normal = mode == DeviceMode.Normal;
            bool recoveryOrDfu = mode == DeviceMode.Recovery || mode == DeviceMode.Dfu;

            // Svako dugme zavisi SAMO od alata koji mu stvarno treba — ne od svih.
            // Tako Info/Backup/Recovery rade i dok npr. idevicerestore još nije tu.
            bool Has(string t) => ToolManager.Find(t) != null;

            btnRefresh.IsEnabled = idle;
            btnInfo.IsEnabled = idle && Has("ideviceinfo") && normal;
            // Dovoljan je ideviceinfo; idevicediagnostics je dodatak (zdravlje baterije).
            btnDiagnostics.IsEnabled = idle && Has("ideviceinfo") && normal;
            btnUpdate.IsEnabled = idle && Has("idevicerestore") && anyDevice;
            btnRestore.IsEnabled = idle && Has("idevicerestore") && anyDevice;
            btnBackup.IsEnabled = idle && Has("idevicebackup2") && normal;
            btnRestoreBackup.IsEnabled = idle && Has("idevicebackup2") && normal;
            btnRecovery.IsEnabled = idle && Has("ideviceenterrecovery") && normal;
            btnExitRecovery.IsEnabled = idle && Has("irecovery") && recoveryOrDfu;
            btnDfu.IsEnabled = idle;             // vodič radi i bez uređaja
            btnFiles.IsEnabled = idle && Has("afcclient") && normal;
            btnTrust.IsEnabled = idle && Has("idevicepair") && normal;
            btnRename.IsEnabled = idle && Has("idevicename") && normal;
            btnRestart.IsEnabled = idle && Has("idevicediagnostics") && normal;
            btnShutdown.IsEnabled = idle && Has("idevicediagnostics") && normal;
            btnVoiceOverOn.IsEnabled = idle && Has("ideviceaccessibility") && normal;
            btnVoiceOverOff.IsEnabled = idle && Has("ideviceaccessibility") && normal;
            btnApps.IsEnabled = idle && Has("ideviceinstaller") && normal;
            btnSyslog.IsEnabled = idle && Has("idevicesyslog") && normal;
            btnCrash.IsEnabled = idle && Has("idevicecrashreport") && normal;
            btnActivate.IsEnabled = idle && Has("ideviceactivation") && normal;
            btnCancel.IsEnabled = _busy;

            rbLatest.IsEnabled = idle;
            rbIpsw.IsEnabled = idle;
            txtIpswPath.IsEnabled = idle;
            btnBrowseIpsw.IsEnabled = idle && rbIpsw.IsChecked == true;
        }

        private void Firmware_Changed(object sender, RoutedEventArgs e)
        {
            // rbIpsw može biti null tokom inicijalizacije XAML-a
            if (btnBrowseIpsw != null && rbIpsw != null)
                btnBrowseIpsw.IsEnabled = !_busy && rbIpsw.IsChecked == true;
        }

        private void BtnBrowseIpsw_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = LanguageManager.L("pick_ipsw"),
                Filter = "iOS firmware (*.ipsw)|*.ipsw|All files (*.*)|*.*",
                InitialDirectory = Directory.Exists(_settings.LastIpswFolder) ? _settings.LastIpswFolder : ""
            };
            if (dlg.ShowDialog(this) == true)
            {
                txtIpswPath.Text = dlg.FileName;
                _settings.LastIpswFolder = Path.GetDirectoryName(dlg.FileName) ?? "";
                _settings.Save();
                rbIpsw.IsChecked = true;
            }
        }

        // ════════════════════════════════════════════════════════════════
        // NAPREDAK
        // ════════════════════════════════════════════════════════════════
        private void SetProgress(string text, bool speak)
        {
            if (speak && _settings.SpeakProgress) Announcer.Say(txbProgress, text);
            else txbProgress.Text = text;
        }

        /// <summary>Prevodi stabilan ključ faze (iz DeviceEngine) u čitljiv naziv; prazno ako ga nema.</summary>
        private static string PhaseLabel(string stepKey)
        {
            if (string.IsNullOrEmpty(stepKey)) return "";
            string key = "phase_" + stepKey;
            string s = LanguageManager.L(key);
            return s == key ? stepKey : s;   // ako prevod ne postoji, prikaži sam ključ
        }

        private void OnEngineProgress(DeviceEngine.Progress p)
        {
            // Zove se sa pozadinske niti (izlaz procesa) — vrati na UI nit.
            Dispatcher.Invoke(() =>
            {
                prgOverall.Value = p.Percent >= 0 ? p.Percent : 0;

                string pctText = p.Percent >= 0 ? $"{p.Percent:F0}%" : "";
                string label = PhaseLabel(p.Step);
                string text = string.IsNullOrEmpty(label)
                    ? pctText
                    : (string.IsNullOrEmpty(pctText) ? label : $"{label}, {pctText}");

                // Ne pričaj svaki procenat — samo na promenu koraka ili svakih ~10%.
                bool stepChanged = p.Step != _lastSpokenStep;
                bool bigJump = p.Percent < 0 || Math.Abs(p.Percent - _lastSpokenPct) >= 10;
                bool speak = _settings.SpeakProgress && (stepChanged || bigJump);
                if (speak) { _lastSpokenStep = p.Step; _lastSpokenPct = p.Percent; }

                SetProgress(text, speak);
            });
        }

        // ════════════════════════════════════════════════════════════════
        // POKRETANJE DUGAČKIH RADNJI
        // ════════════════════════════════════════════════════════════════
        private async Task RunOperation(string startKey, string doneKey, Func<CancellationToken, Task> work,
            bool refreshAfter = true, bool rebootAfter = false)
        {
            if (_busy) return;
            _busy = true;
            bool ok = false;
            _lastSpokenPct = -100; _lastSpokenStep = "";
            _cts = new CancellationTokenSource();
            prgOverall.Value = 0;
            UpdateActionStates();
            SetProgress(LanguageManager.L(startKey), speak: true);
            Logger.Section(startKey);

            try
            {
                await work(_cts.Token);
                ok = true;
                prgOverall.Value = 100;
                // Kod restore/update-a poruku o završetku daje RefreshAfterRestoreAsync
                // (najava reboota), da se ne bi izgovorile dve slične poruke zaredom.
                if (!rebootAfter) SetProgress(LanguageManager.L(doneKey), speak: true);
            }
            catch (OperationCanceledException)
            {
                SetProgress(LanguageManager.L("cancelled"), speak: true);
            }
            catch (RestoreException rex)
            {
                string explain = RestoreErrors.ExplainMessage(rex.Message, rex.AppleErrorCode, LanguageManager.Current);
                string msg = rex.Message + "\n\n" + explain;
                Logger.Write("RESTORE FAILED: " + rex.Message + " | code=" + (rex.AppleErrorCode?.ToString() ?? "?"));
                SetProgress(explain, speak: true);
                Dialogs.Error(this, "Ultra iDevice", msg);
            }
            catch (Exception ex)
            {
                Logger.Write("OPERATION FAILED: " + ex.Message);
                SetProgress(ex.Message, speak: true);
                Dialogs.Error(this, "Ultra iDevice", ex.Message);
            }
            finally
            {
                _busy = false;
                _cts?.Dispose();
                _cts = null;
                if (ok && rebootAfter) await RefreshAfterRestoreAsync();
                else if (refreshAfter) await RefreshDeviceAsync();
                else UpdateActionStates();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            try { _cts?.Cancel(); } catch { }
        }

        // ── Pojedinačne radnje ───────────────────────────────────────────
        private async void BtnInfo_Click(object sender, RoutedEventArgs e)
        {
            if (_device.Mode != DeviceMode.Normal)
            {
                Dialogs.Info(this, LanguageManager.L("no_device_title"), LanguageManager.L("no_device_msg"));
                return;
            }
            bool sr = LanguageManager.Current == "sr";
            SetProgress(sr ? "Čitam podatke o uređaju..." : "Reading device information...", speak: true);

            DeviceInfo d;
            try { d = await DeviceEngine.GetFullReportAsync(_device.Udid); }
            catch (Exception ex) { Dialogs.Error(this, "Ultra iDevice", ex.Message); SetProgress(LanguageManager.L("status_ready"), false); return; }

            var sb = new System.Text.StringBuilder();
            void Line(string srL, string enL, string val)
            {
                if (!string.IsNullOrWhiteSpace(val)) sb.AppendLine((sr ? srL : enL) + ": " + val);
            }

            Line("Model", "Model", d.FriendlyModel + $" ({d.ProductType})");
            Line("iOS verzija", "iOS version",
                d.ProductVersion + (string.IsNullOrEmpty(d.BuildVersion) ? "" : $" ({d.BuildVersion})"));
            Line("Ime uređaja", "Device name", d.DeviceName);

            if (d.BatteryPercent >= 0)
            {
                string b = d.BatteryPercent + "%" + (d.BatteryCharging ? (sr ? ", puni se" : ", charging") : "");
                Line("Baterija", "Battery", b);
            }
            if (d.StorageTotalBytes > 0)
            {
                string s = (sr ? "slobodno " : "free ") + DeviceInfo.Gb(d.StorageFreeBytes) +
                           " / " + DeviceInfo.Gb(d.StorageTotalBytes);
                Line("Memorija", "Storage", s);
            }

            Line("Broj telefona", "Phone number", d.PhoneNumber);
            Line("Model broj", "Model number", d.ModelNumber);
            Line("Region", "Region", d.RegionInfo);
            Line("Arhitektura procesora", "CPU architecture", d.CpuArchitecture);
            Line("Aktivacija", "Activation", d.ActivationState);
            Line("Wi-Fi adresa", "Wi-Fi address", d.WiFiAddress);
            Line("Bluetooth adresa", "Bluetooth address", d.BluetoothAddress);
            Line("Serijski broj", "Serial number", d.SerialNumber);
            sb.AppendLine("UDID: " + d.Udid);

            SetProgress(LanguageManager.L("status_ready"), false);
            Dialogs.ShowText(this, LanguageManager.L("btn_info"), sb.ToString().TrimEnd());
        }

        private async void BtnDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            if (_device.Mode != DeviceMode.Normal)
            {
                Dialogs.Info(this, LanguageManager.L("no_device_title"), LanguageManager.L("no_device_msg"));
                return;
            }
            SetProgress(LanguageManager.L("diag_running"), speak: true);
            string report;
            try { report = await DeviceEngine_RunDiagnostics(_device.Udid); }
            catch (Exception ex) { Dialogs.Error(this, "Ultra iDevice", ex.Message); SetProgress(LanguageManager.L("status_ready"), false); return; }

            SetProgress(LanguageManager.L("status_ready"), false);
            Dialogs.ShowText(this, LanguageManager.L("diag_title"), report);
        }

        // Ceo dijagnostički posao ide na POZADINSKU nit (Task.Run), da ne blokira
        // UI. Bez ovoga, sinhroni poziv unutra zaključa glavnu nit (deadlock).
        private static Task<string> DeviceEngine_RunDiagnostics(string udid) =>
            Task.Run(() => DiagnosticsEngine.BuildReportAsync(udid));

        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureDevice()) return;
            if (!Dialogs.Confirm(this, LanguageManager.L("confirm_update_title"),
                    LanguageManager.L("confirm_update_msg", DeviceLabel()))) return;
            bool latest = rbLatest.IsChecked == true;
            string ipsw = txtIpswPath.Text;
            await RunOperation("running_update", "done_update",
                ct => DeviceEngine.RunRestoreAsync(erase: false, latestSigned: latest, ipswPath: ipsw, OnEngineProgress, ct),
                rebootAfter: true);
        }

        private async void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureDevice()) return;
            if (!Dialogs.Confirm(this, LanguageManager.L("confirm_restore_title"),
                    LanguageManager.L("confirm_restore_msg", DeviceLabel()))) return;
            bool latest = rbLatest.IsChecked == true;
            string ipsw = txtIpswPath.Text;
            await RunOperation("running_restore", "done_restore",
                ct => DeviceEngine.RunRestoreAsync(erase: true, latestSigned: latest, ipswPath: ipsw, OnEngineProgress, ct),
                rebootAfter: true);
        }

        private async void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureNormal()) return;
            string folder = PickFolder(LanguageManager.L("pick_backup_folder"), _settings.BackupFolder);
            if (folder == null) return;
            _settings.BackupFolder = folder; _settings.Save();
            string udid = _device.Udid;
            await RunOperation("running_backup", "done_backup",
                ct => DeviceEngine.BackupAsync(udid, folder, OnEngineProgress, ct));
        }

        private async void BtnRestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureNormal()) return;
            string folder = PickFolder(LanguageManager.L("pick_restore_folder"), _settings.BackupFolder);
            if (folder == null) return;
            if (!Dialogs.Confirm(this, LanguageManager.L("confirm_restore_backup_title"),
                    LanguageManager.L("confirm_restore_backup_msg", DeviceLabel()))) return;
            string udid = _device.Udid;
            await RunOperation("running_restore_backup", "done_restore_backup",
                ct => DeviceEngine.RestoreBackupAsync(udid, folder, OnEngineProgress, ct));
        }

        private async void BtnRecovery_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureNormal()) return;
            string udid = _device.Udid;
            await RunOperation("running_update", "recovery_entered",
                ct => DeviceEngine.EnterRecoveryAsync(udid, ct));
        }

        private async void BtnExitRecovery_Click(object sender, RoutedEventArgs e)
        {
            await RunOperation("running_update", "recovery_exited",
                ct => DeviceEngine.ExitRecoveryAsync(ct));
        }

        private void BtnDfu_Click(object sender, RoutedEventArgs e)
        {
            var win = new DfuGuideWindow(this, _device);
            win.ShowDialog();
        }

        private void BtnFiles_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || !EnsureNormal()) return;
            if (!AfcEngine.Available)
            {
                Dialogs.Info(this, LanguageManager.L("btn_files"), LanguageManager.L("no_device_msg"));
                return;
            }
            var win = new FileManagerWindow(this, _device);
            win.ShowDialog();
        }

        private async void BtnTrust_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || !EnsureNormal()) return;
            bool sr = LanguageManager.Current == "sr";
            Dialogs.Info(this, LanguageManager.L("btn_trust"), sr
                ? "Na telefonu će se pojaviti pitanje „Veruj ovom računaru?“ — dodirni Veruj i unesi šifru ako traži, pa klikni U redu ovde."
                : "Your phone will ask \"Trust This Computer?\" — tap Trust and enter the passcode if asked, then click OK here.");
            SetProgress(sr ? "Uparivanje..." : "Pairing...", speak: true);
            try
            {
                string msg = await DeviceEngine.PairAsync(_device.Udid, System.Threading.CancellationToken.None);
                SetProgress(LanguageManager.L("status_ready"), false);
                Dialogs.Info(this, LanguageManager.L("btn_trust"), msg);
            }
            catch (Exception ex)
            {
                SetProgress(LanguageManager.L("status_ready"), false);
                Dialogs.Error(this, "Ultra iDevice", ex.Message);
            }
            await RefreshDeviceAsync();
        }

        private async void BtnRename_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || !EnsureNormal()) return;
            bool sr = LanguageManager.Current == "sr";
            string name = Dialogs.Prompt(this, LanguageManager.L("btn_rename"),
                sr ? "Novo ime uređaja:" : "New device name:", _device.DeviceName);
            if (string.IsNullOrWhiteSpace(name)) return;

            SetProgress(sr ? "Menjam ime..." : "Renaming...", speak: true);
            try { await DeviceEngine.RenameAsync(_device.Udid, name.Trim(), System.Threading.CancellationToken.None); }
            catch (Exception ex)
            {
                SetProgress(LanguageManager.L("status_ready"), false);
                Dialogs.Error(this, "Ultra iDevice", ex.Message);
                return;
            }
            await RefreshDeviceAsync();
            SetProgress(LanguageManager.L("status_ready"), false);
        }

        private async void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || !EnsureNormal()) return;
            bool sr = LanguageManager.Current == "sr";
            if (!Dialogs.Confirm(this, LanguageManager.L("btn_restart"),
                sr ? "Restartovati uređaj?" : "Restart the device?")) return;

            SetProgress(sr ? "Restartujem uređaj..." : "Restarting device...", speak: true);
            try { await DeviceEngine.RestartAsync(_device.Udid, System.Threading.CancellationToken.None); }
            catch (Exception ex)
            {
                SetProgress(LanguageManager.L("status_ready"), false);
                Dialogs.Error(this, "Ultra iDevice", ex.Message);
                return;
            }
            SetProgress(sr ? "Uređaj se restartuje." : "The device is restarting.", speak: true);
            await RefreshDeviceAsync();
        }

        private async void BtnShutdown_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || !EnsureNormal()) return;
            bool sr = LanguageManager.Current == "sr";
            if (!Dialogs.Confirm(this, LanguageManager.L("btn_shutdown"),
                sr ? "Isključiti uređaj?" : "Shut down the device?")) return;

            SetProgress(sr ? "Isključujem uređaj..." : "Shutting down device...", speak: true);
            try { await DeviceEngine.ShutdownAsync(_device.Udid, System.Threading.CancellationToken.None); }
            catch (Exception ex)
            {
                SetProgress(LanguageManager.L("status_ready"), false);
                Dialogs.Error(this, "Ultra iDevice", ex.Message);
                return;
            }
            SetProgress(sr ? "Uređaj se isključuje." : "The device is shutting down.", speak: true);
            await RefreshDeviceAsync();
        }

        private void BtnApps_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || !EnsureNormal()) return;
            var win = new AppsWindow(this, _device);
            win.ShowDialog();
        }

        private void BtnSyslog_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || !EnsureNormal()) return;
            var win = new SyslogWindow(this, _device);
            win.ShowDialog();
        }

        private async void BtnCrash_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || !EnsureNormal()) return;
            bool sr = LanguageManager.Current == "sr";
            string folder = PickFolder(sr ? "Izaberi folder za crash logove" : "Choose a folder for crash logs", _settings.BackupFolder);
            if (folder == null) return;

            SetProgress(sr ? "Izvozim crash logove..." : "Exporting crash logs...", speak: true);
            try
            {
                await DeviceEngine.ExportCrashLogsAsync(_device.Udid, folder, System.Threading.CancellationToken.None);
                SetProgress(sr ? "Crash logovi izvezeni." : "Crash logs exported.", speak: true);
                // Odmah otvori čitač nad tim folderom.
                new CrashLogWindow(this, folder).ShowDialog();
            }
            catch (Exception ex)
            {
                SetProgress(LanguageManager.L("status_ready"), false);
                Dialogs.Error(this, "Ultra iDevice", ex.Message);
            }
        }

        private async void BtnActivate_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || !EnsureNormal()) return;
            bool sr = LanguageManager.Current == "sr";
            SetProgress(sr ? "Aktiviram uređaj..." : "Activating device...", speak: true);
            try
            {
                string msg = await DeviceEngine.ActivateAsync(_device.Udid, System.Threading.CancellationToken.None);
                SetProgress(sr ? "Uređaj aktiviran." : "Device activated.", speak: true);
                Dialogs.Info(this, LanguageManager.L("btn_activate"), string.IsNullOrWhiteSpace(msg) ? (sr ? "Uređaj aktiviran." : "Device activated.") : msg);
            }
            catch (Exception ex)
            {
                SetProgress(LanguageManager.L("status_ready"), false);
                Dialogs.Error(this, "Ultra iDevice", ex.Message);
            }
            await RefreshDeviceAsync();
        }

        private async void BtnVoiceOverOn_Click(object sender, RoutedEventArgs e) => await SetVoiceOver(true);
        private async void BtnVoiceOverOff_Click(object sender, RoutedEventArgs e) => await SetVoiceOver(false);

        private async System.Threading.Tasks.Task SetVoiceOver(bool on)
        {
            if (_busy || !EnsureNormal()) return;
            bool sr = LanguageManager.Current == "sr";
            SetProgress(on
                ? (sr ? "Uključujem VoiceOver na telefonu..." : "Turning on VoiceOver on the phone...")
                : (sr ? "Isključujem VoiceOver na telefonu..." : "Turning off VoiceOver on the phone..."),
                speak: true);
            try
            {
                await DeviceEngine.SetVoiceOverAsync(_device.Udid, on, System.Threading.CancellationToken.None);
                SetProgress(on
                    ? (sr ? "VoiceOver uključen na telefonu." : "VoiceOver turned on on the phone.")
                    : (sr ? "VoiceOver isključen na telefonu." : "VoiceOver turned off on the phone."),
                    speak: true);
            }
            catch (Exception ex)
            {
                SetProgress(LanguageManager.L("status_ready"), false);
                Dialogs.Error(this, "Ultra iDevice", ex.Message);
            }
        }

        // ── Pomoćno za radnje ─────────────────────────────────────────────
        private bool EnsureDevice()
        {
            if (_device.Mode == DeviceMode.None)
            {
                Dialogs.Info(this, LanguageManager.L("no_device_title"), LanguageManager.L("no_device_msg"));
                return false;
            }
            return true;
        }

        private bool EnsureNormal()
        {
            if (_device.Mode != DeviceMode.Normal)
            {
                Dialogs.Info(this, LanguageManager.L("no_device_title"), LanguageManager.L("no_device_msg"));
                return false;
            }
            return true;
        }

        private string DeviceLabel() =>
            string.IsNullOrEmpty(_device.FriendlyModel) ? (LanguageManager.Current == "sr" ? "uređaj" : "the device") : _device.FriendlyModel;

        private string PickFolder(string description, string initial)
        {
            using var dlg = new WinForms.FolderBrowserDialog
            {
                Description = description,
                UseDescriptionForTitle = true,
                SelectedPath = Directory.Exists(initial) ? initial : ""
            };
            return dlg.ShowDialog() == WinForms.DialogResult.OK ? dlg.SelectedPath : null;
        }

        // ════════════════════════════════════════════════════════════════
        // ALATI
        // ════════════════════════════════════════════════════════════════
        private async void MnuInstallTools_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            _busy = true; UpdateActionStates();
            SetProgress(LanguageManager.L("tools_installing"), speak: true);
            try
            {
                await ToolManager.InstallToolsAsync((pct, note) =>
                    Dispatcher.Invoke(() =>
                    {
                        prgOverall.Value = pct;
                        SetProgress(LanguageManager.L("tools_installing") + (string.IsNullOrEmpty(note) ? "" : "  " + note), speak: false);
                    }));
                prgOverall.Value = 100;
                var missing = ToolManager.MissingTools();
                if (missing.Count == 0)
                {
                    SetProgress(LanguageManager.L("tools_installed"), speak: true);
                    Dialogs.Info(this, LanguageManager.L("menu_tools"), LanguageManager.L("tools_installed"));
                }
                else
                {
                    // Nešto se skinulo, ali ne svi osnovni alati (npr. zip nema idevicerestore).
                    bool sr = LanguageManager.Current == "sr";
                    string msg = (sr
                        ? "Preuzimanje je prošlo, ali još fale ovi alati:\n  "
                        : "The download finished, but these tools are still missing:\n  ")
                        + string.Join(", ", missing)
                        + "\n\n" + MissingToolsHint();
                    SetProgress(LanguageManager.L("status_no_tools"), speak: true);
                    Dialogs.Error(this, LanguageManager.L("tools_missing_title"), msg);
                }
            }
            catch (Exception ex)
            {
                string msg = LanguageManager.L("tools_install_failed", ToolManager.UserToolsDir, ex.Message)
                    + "\n\n" + MissingToolsHint();
                SetProgress(LanguageManager.L("status_no_tools"), speak: true);
                Dialogs.Error(this, LanguageManager.L("tools_missing_title"), msg);
            }
            finally
            {
                _busy = false;
                await RefreshDeviceAsync();
            }
        }

        private string MissingToolsHint()
        {
            bool sr = LanguageManager.Current == "sr";
            return sr
                ? "Dve mogućnosti:\n" +
                  "1) Ubaci .exe i .dll alate u ovaj folder, pa ih aplikacija nalazi:\n   " + ToolManager.UserToolsDir + "\n" +
                  "   (ili u folder \"tools\" pored exe-ja / u korenu projekta za pokretanje iz Visual Studija).\n" +
                  "2) Promeni izvor preuzimanja bez prevođenja: napravi fajl\n   " + ToolManager.SourceFile + "\n" +
                  "   i u njega upiši URL do zip-a, putanju do lokalnog zip-a, ili putanju do foldera sa alatima."
                : "Two options:\n" +
                  "1) Put the tool .exe and .dll files into this folder; the app will find them:\n   " + ToolManager.UserToolsDir + "\n" +
                  "   (or a \"tools\" folder next to the exe / in the project root for running from Visual Studio).\n" +
                  "2) Change the download source without rebuilding: create the file\n   " + ToolManager.SourceFile + "\n" +
                  "   containing a URL to a zip, a path to a local zip, or a path to a folder of tools.";
        }

        private void MnuCheckDriver_Click(object sender, RoutedEventArgs e)
        {
            if (ToolManager.AppleDriverPresent())
                Dialogs.Info(this, LanguageManager.L("tools_check_driver"), LanguageManager.L("driver_ok"));
            else
                Dialogs.Info(this, LanguageManager.L("driver_missing_title"), LanguageManager.L("driver_missing_msg"));
        }

        private void MnuOpenLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(Logger.LogsDir);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Logger.LogsDir) { UseShellExecute = true });
            }
            catch (Exception ex) { Dialogs.Error(this, "Ultra iDevice", ex.Message); }
        }

        private void MnuSpeakProgress_Click(object sender, RoutedEventArgs e)
        {
            _settings.SpeakProgress = mnuSpeakProgress.IsChecked;
            _settings.Save();
        }

        // ════════════════════════════════════════════════════════════════
        // POMOĆ
        // ════════════════════════════════════════════════════════════════
        private void MnuShortcuts_Click(object sender, RoutedEventArgs e)
        {
            bool sr = LanguageManager.Current == "sr";
            string body = sr
                ? "F1 — ova lista prečica\n" +
                  "F5 — osveži uređaj\n" +
                  "Ctrl+plus / Ctrl+minus / Ctrl+0 — veličina teksta\n" +
                  "Alt + podvučeno slovo — meniji i dugmad\n" +
                  "Esc — zatvori dijalog\n\n" +
                  "U vođenom DFU prozoru: Start pokreće govorni vodič, Nazad/Napred za ručni tempo, Proveri sada za detekciju moda."
                : "F1 — this shortcut list\n" +
                  "F5 — refresh device\n" +
                  "Ctrl+plus / Ctrl+minus / Ctrl+0 — text size\n" +
                  "Alt + underlined letter — menus and buttons\n" +
                  "Esc — close a dialog\n\n" +
                  "In the guided DFU window: Start runs the spoken guide, Previous/Next for manual pace, Detect now to check the mode.";
            Dialogs.ShowText(this, LanguageManager.L("shortcuts_title"), body);
        }

        private void MnuErrorCodes_Click(object sender, RoutedEventArgs e)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var entry in RestoreErrors.All)
                sb.AppendLine(LanguageManager.Current == "sr" ? entry.Sr : entry.En).AppendLine();
            Dialogs.ShowText(this, LanguageManager.L("help_errorcodes"), sb.ToString().TrimEnd());
        }

        private void MnuSupport_Click(object sender, RoutedEventArgs e) => SupportWindow.Show(this);

        private void MnuAbout_Click(object sender, RoutedEventArgs e)
        {
            bool sr = LanguageManager.Current == "sr";
            string body = sr
                ? $"Ultra iDevice {GetAppVersion()}\n\n" +
                  "Pristupačan alat za restore, update i backup iPhone/iPad uređaja. Deo Ultra porodice programa.\n\n" +
                  "Koristi open-source alate libimobiledevice i idevicerestore. Na Windows-u je potreban Apple USB drajver (dolazi uz aplikaciju Apple Devices ili iTunes).\n\n" +
                  "Autor: Demir Ajvazi.\n" +
                  "Licenca: GPL-3.0."
                : $"Ultra iDevice {GetAppVersion()}\n\n" +
                  "Accessible iPhone/iPad restore, update and backup. Part of the Ultra family of apps.\n\n" +
                  "Uses the open-source libimobiledevice and idevicerestore tools. On Windows it needs the Apple USB driver (comes with the Apple Devices app or iTunes).\n\n" +
                  "Author: Demir Ajvazi.\n" +
                  "License: GPL-3.0.";
            Dialogs.ShowText(this, LanguageManager.L("about_title"), body);
        }

        private void MnuExit_Click(object sender, RoutedEventArgs e) => Close();

        // ════════════════════════════════════════════════════════════════
        // PREČICE + ZATVARANJE
        // ════════════════════════════════════════════════════════════════
        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == System.Windows.Input.Key.F1) { MnuShortcuts_Click(this, null); e.Handled = true; }
            else if (e.Key == System.Windows.Input.Key.F5 && !_busy) { _ = RefreshDeviceAsync(); e.Handled = true; }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_busy)
            {
                bool sr = LanguageManager.Current == "sr";
                string msg = sr
                    ? "Radnja je u toku (npr. restore ili backup). Ako sada zatvoriš, to može oštetiti uređaj. Stvarno zatvoriti?"
                    : "An operation is in progress (e.g. restore or backup). Closing now could damage the device. Really close?";
                if (!Dialogs.Confirm(this, "Ultra iDevice", msg)) { e.Cancel = true; return; }
                try { _cts?.Cancel(); } catch { }
            }
        }
    }
}
