using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace UltraIDevice
{
    /// <summary>
    /// Vođeni, GOVORNI prolazak kroz korake za ulazak u DFU ili recovery mod.
    /// Namenjen pre svega slepom korisniku: kad pritisne Start, aplikacija sama
    /// prolazi kroz korake tačnim tajmingom — izgovara svaku radnju u pravom
    /// trenutku i odbrojava dok se drži dugmad — tako da korisnik ne mora da gleda
    /// ekran ni da pritiska tastere na računaru dok drži dugmad na telefonu.
    ///
    /// Istovremeno je upotrebljiv i za videće: svaki korak je i vidljiv, a tu su i
    /// dugmad Nazad/Napred za ručni tempo.
    /// </summary>
    public sealed class DfuGuideWindow : Window
    {
        private readonly bool _sr = LanguageManager.Current == "sr";
        private DfuFamily _family;
        private bool _recoveryMode;          // false = DFU, true = recovery
        private List<DfuStep> _steps = new();
        private int _index = -1;             // trenutni korak (-1 pre starta)
        private int _remaining;              // sekunde preostale u tekućem koraku

        private readonly DispatcherTimer _playTimer;
        private readonly DispatcherTimer _detectTimer;
        private bool _detecting;             // reentrancy brava za detekciju
        private bool _playing;

        // Kontrole
        private readonly RadioButton _rbDfu, _rbRecovery;
        private readonly ComboBox _cmbFamily;
        private readonly TextBlock _txtStep;     // assertive live region
        private readonly TextBlock _txtCount;    // polite live region
        private readonly TextBlock _txtResult;   // assertive live region
        private readonly Button _btnStart, _btnPrev, _btnNext, _btnDetect;

        public DfuGuideWindow(Window owner, DeviceInfo detected)
        {
            Owner = owner;
            Title = _sr ? "Vođeni DFU / recovery mod" : "Guided DFU / recovery mode";
            Width = 600; Height = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = ThemeBrushes.MainWindowBg;
            Foreground = ThemeBrushes.TextPrimary;

            _family = DfuGuide.FamilyFor(detected?.ProductType);

            var panel = new StackPanel { Margin = new Thickness(18) };

            var heading = new TextBlock
            {
                Text = _sr ? "Vođeni DFU / recovery mod" : "Guided DFU / recovery mode",
                FontSize = 17, FontWeight = FontWeights.Bold,
                Foreground = ThemeBrushes.TextPrimary, Margin = new Thickness(0, 0, 0, 10),
            };
            System.Windows.Automation.AutomationProperties.SetHeadingLevel(heading, System.Windows.Automation.AutomationHeadingLevel.Level1);
            panel.Children.Add(heading);

            // Šta ulazimo: DFU ili recovery
            panel.Children.Add(new Label { Content = _sr ? "Šta ulaziš:" : "What to enter:", Foreground = ThemeBrushes.TextPrimary });
            _rbDfu = new RadioButton { Content = "DFU", GroupName = "mode", IsChecked = true, Foreground = ThemeBrushes.TextPrimary, Margin = new Thickness(0, 0, 0, 2) };
            _rbRecovery = new RadioButton { Content = _sr ? "Recovery mod" : "Recovery mode", GroupName = "mode", Foreground = ThemeBrushes.TextPrimary, Margin = new Thickness(0, 0, 0, 6) };
            _rbDfu.Checked += (s, e) => { _recoveryMode = false; ResetSteps(); };
            _rbRecovery.Checked += (s, e) => { _recoveryMode = true; ResetSteps(); };
            panel.Children.Add(_rbDfu);
            panel.Children.Add(_rbRecovery);

            // Familija uređaja
            panel.Children.Add(new Label { Content = _sr ? "Model uređaja:" : "Device model:", Foreground = ThemeBrushes.TextPrimary });
            _cmbFamily = new ComboBox { Margin = new Thickness(0, 0, 0, 10) };
            foreach (DfuFamily f in new[] { DfuFamily.FaceId, DfuFamily.IPhone7, DfuFamily.Legacy })
                _cmbFamily.Items.Add(new ComboBoxItem { Content = DfuGuide.FamilyName(f, LanguageManager.Current), Tag = f });
            _cmbFamily.SelectedIndex = _family switch { DfuFamily.FaceId => 0, DfuFamily.IPhone7 => 1, _ => 2 };
            System.Windows.Automation.AutomationProperties.SetName(_cmbFamily, _sr ? "Model uređaja" : "Device model");
            _cmbFamily.SelectionChanged += (s, e) =>
            {
                if (_cmbFamily.SelectedItem is ComboBoxItem it && it.Tag is DfuFamily f) { _family = f; ResetSteps(); }
            };
            panel.Children.Add(_cmbFamily);

            // Tekući korak
            _txtStep = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap, FontSize = 16, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeBrushes.TextPrimary, MinHeight = 60, Margin = new Thickness(0, 4, 0, 6),
            };
            System.Windows.Automation.AutomationProperties.SetLiveSetting(_txtStep, System.Windows.Automation.AutomationLiveSetting.Assertive);
            panel.Children.Add(_txtStep);

            // Odbrojavanje
            _txtCount = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap, FontSize = 15, Foreground = ThemeBrushes.AccentBrush, MinHeight = 24,
            };
            System.Windows.Automation.AutomationProperties.SetLiveSetting(_txtCount, System.Windows.Automation.AutomationLiveSetting.Polite);
            panel.Children.Add(_txtCount);

            // Rezultat (detekcija)
            _txtResult = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap, FontSize = 14, Foreground = ThemeBrushes.TextSecondary,
                Margin = new Thickness(0, 6, 0, 10), MinHeight = 24,
            };
            System.Windows.Automation.AutomationProperties.SetLiveSetting(_txtResult, System.Windows.Automation.AutomationLiveSetting.Assertive);
            panel.Children.Add(_txtResult);

            // Dugmad
            var row = new WrapPanel();
            _btnStart = Btn(_sr ? "Start" : "Start", () => StartPlay());
            _btnPrev = Btn(_sr ? "Nazad" : "Previous", () => ManualStep(-1));
            _btnNext = Btn(_sr ? "Napred" : "Next", () => ManualStep(1));
            _btnDetect = BtnAsync(_sr ? "Proveri sada" : "Detect now", () => DetectOnce(true));
            var close = Btn(_sr ? "Zatvori" : "Close", () => Close());
            close.IsCancel = true;
            row.Children.Add(_btnStart);
            row.Children.Add(_btnPrev);
            row.Children.Add(_btnNext);
            row.Children.Add(_btnDetect);
            row.Children.Add(close);
            panel.Children.Add(row);

            Content = panel;

            _playTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _playTimer.Tick += PlayTick;
            _detectTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _detectTimer.Tick += async (s, e) => await DetectOnce(false);

            KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
            Closed += (s, e) => { _playTimer.Stop(); _detectTimer.Stop(); };

            ResetSteps();
            UiScaling.Register(this);
        }

        private Button Btn(string content, Action onClick)
        {
            var b = new Button { Content = content, Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 0, 8, 8), Cursor = System.Windows.Input.Cursors.Hand };
            b.Click += (s, e) => onClick();
            return b;
        }
        private Button BtnAsync(string content, Func<System.Threading.Tasks.Task> onClick)
        {
            var b = new Button { Content = content, Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 0, 8, 8), Cursor = System.Windows.Input.Cursors.Hand };
            b.Click += async (s, e) => await onClick();
            return b;
        }

        private void ResetSteps()
        {
            _playTimer.Stop();
            _detectTimer.Stop();
            _playing = false;
            _index = -1;
            _steps = _recoveryMode
                ? DfuGuide.GetRecoverySteps(_family, LanguageManager.Current)
                : DfuGuide.GetDfuSteps(_family, LanguageManager.Current);
            _txtCount.Text = "";
            _txtResult.Text = "";
            // Prikaži prvi korak (obično "poveži kablom") i čekaj Start.
            if (_steps.Count > 0)
                Announcer.Say(_txtStep, _steps[0].Text + "  " + (_sr ? "Pritisni Start kad budeš spreman." : "Press Start when ready."));
        }

        private void StartPlay()
        {
            if (_steps.Count == 0) return;
            _detectTimer.Stop();
            _txtResult.Text = "";
            _playing = true;
            // Autoplej kreće od prvog koraka koji je radnja (indeks 1); korak 0 je priprema.
            GoToStep(_steps.Count > 1 ? 1 : 0);
        }

        private void GoToStep(int i)
        {
            _playTimer.Stop();
            _index = Math.Clamp(i, 0, _steps.Count - 1);
            var step = _steps[_index];
            string prefix = LanguageManager.L("step_of", _index + 1, _steps.Count, step.Text);
            Announcer.Say(_txtStep, prefix);
            _txtCount.Text = "";

            if (!_playing)
                return; // ručni režim: samo prikaži korak

            if (_index >= _steps.Count - 1)
            {
                // Poslednji korak = poruka o rezultatu. Prestani s plejom i kreni detekciju.
                _playing = false;
                StartDetecting();
                return;
            }

            // Trajanje koraka: hold sekunde, ili kratak prozor za "brzi" korak.
            _remaining = step.HoldSeconds > 0 ? step.HoldSeconds : 2;
            _playTimer.Start();
        }

        private void PlayTick(object sender, EventArgs e)
        {
            var step = _steps[_index];
            if (step.HoldSeconds > 0)
                Announcer.Say(_txtCount, (_sr ? "Drži... " : "Hold... ") + _remaining);

            _remaining--;
            if (_remaining <= 0)
            {
                _playTimer.Stop();
                GoToStep(_index + 1);
            }
        }

        private void ManualStep(int delta)
        {
            // Ručni tempo: ugasi autoplej i pomeri se jedan korak.
            _playing = false;
            _playTimer.Stop();
            if (_index < 0) _index = 0;
            GoToStep(_index + delta);
        }

        private void StartDetecting()
        {
            Announcer.Say(_txtResult, _sr ? "Proveravam da li je uređaj ušao u mod..." : "Checking whether the device entered the mode...");
            _detectTimer.Start();
        }

        private async System.Threading.Tasks.Task DetectOnce(bool manual)
        {
            if (_detecting) return;
            _detecting = true;
            try
            {
                var info = await DeviceEngine.DetectAsync();
                bool hit = _recoveryMode ? info.Mode == DeviceMode.Recovery : info.Mode == DeviceMode.Dfu;
                // DFU je teško pouzdano razlikovati od recovery-ja na svim build-ovima;
                // ako smo ciljali DFU a alat javi recovery, i to je napredak — javi oba.
                if (_recoveryMode && info.Mode == DeviceMode.Recovery)
                    hit = true;

                if (hit)
                {
                    _detectTimer.Stop();
                    Announcer.Say(_txtResult, _recoveryMode
                        ? (_sr ? "Uspeh: uređaj je u recovery modu. Možeš zatvoriti ovaj prozor i uraditi restore." : "Success: the device is in recovery mode. You can close this window and restore.")
                        : (_sr ? "Uspeh: uređaj je u DFU modu. Možeš zatvoriti ovaj prozor i uraditi restore." : "Success: the device is in DFU mode. You can close this window and restore."));
                }
                else if (manual)
                {
                    string what = info.Mode switch
                    {
                        DeviceMode.Normal => _sr ? "Uređaj je u normalnom radu (nije u DFU/recovery)." : "The device is in normal mode (not DFU/recovery).",
                        DeviceMode.Recovery => _sr ? "Uređaj je u recovery modu." : "The device is in recovery mode.",
                        DeviceMode.Dfu => _sr ? "Uređaj je u DFU modu." : "The device is in DFU mode.",
                        _ => _sr ? "Uređaj se trenutno ne vidi. U DFU modu je ekran crn i to je očekivano — probaj Proveri sada." : "No device visible right now. In DFU the screen is black, which is expected — try Detect now."
                    };
                    Announcer.Say(_txtResult, what);
                }
            }
            catch (Exception ex)
            {
                if (manual) Announcer.Say(_txtResult, (_sr ? "Greška pri proveri: " : "Detection error: ") + ex.Message);
            }
            finally { _detecting = false; }
        }
    }
}
