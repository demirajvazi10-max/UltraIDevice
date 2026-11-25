using System;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace UltraIDevice
{
    /// <summary>
    /// Log uređaja uživo (preko idevicesyslog). Start pokrene tok, Stop ga zaustavi
    /// (ubije proces). Tekst ide u read-only polje koje čitač ekrana može da čita
    /// red po red; ne izgovaramo svaki red uživo (bilo bi prebrzo) — samo status.
    /// Clear briše prikaz, Sačuvaj upiše log u .txt.
    /// </summary>
    public sealed class SyslogWindow : Window
    {
        private readonly bool _sr = LanguageManager.Current == "sr";
        private readonly string _udid;

        private readonly TextBox _text;
        private readonly TextBlock _status;    // assertive live region
        private readonly Button _btnStart, _btnStop, _btnClear, _btnSave;

        private CancellationTokenSource _cts;
        private bool _running;
        private readonly StringBuilder _buffer = new();   // ceo log (za snimanje)

        // idevicesyslog ume da sruči hiljade redova u sekundi. Zato redove NE ispisujemo
        // red-po-red (to zaguši UI nit i sve se zamrzne), nego ih skupljamo ovde i
        // ispisujemo na gomilu na tajmer. OnLine dolazi sa pozadinske niti → lock.
        private readonly object _lock = new();
        private readonly StringBuilder _pending = new();
        private DispatcherTimer _flushTimer;

        private const int MaxViewChars = 200000;    // prikaz: zadrži poslednjih ~200k
        private const int MaxBufferChars = 1000000;  // za snimanje: do ~1M

        public SyslogWindow(Window owner, DeviceInfo device)
        {
            _udid = device?.Udid ?? "";
            Owner = owner;
            Title = _sr ? "Log uređaja (uživo)" : "Device log (live)";
            Width = 720; Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = ThemeBrushes.MainWindowBg;
            Foreground = ThemeBrushes.TextPrimary;

            var grid = new Grid { Margin = new Thickness(16) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var heading = new TextBlock
            {
                Text = _sr ? "Log uređaja (uživo)" : "Device log (live)",
                FontSize = 17, FontWeight = FontWeights.Bold,
                Foreground = ThemeBrushes.TextPrimary, Margin = new Thickness(0, 0, 0, 8),
            };
            System.Windows.Automation.AutomationProperties.SetHeadingLevel(heading, System.Windows.Automation.AutomationHeadingLevel.Level1);
            Grid.SetRow(heading, 0);
            grid.Children.Add(heading);

            _text = new TextBox
            {
                IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = ThemeBrushes.ControlBg, Foreground = ThemeBrushes.TextPrimary,
                BorderBrush = ThemeBrushes.ThemeBorderBrush, FontSize = 12,
                FontFamily = new System.Windows.Media.FontFamily("Consolas, Courier New, monospace"),
                Padding = new Thickness(8),
            };
            System.Windows.Automation.AutomationProperties.SetName(_text, _sr ? "Sadržaj loga" : "Log contents");
            Grid.SetRow(_text, 1);
            grid.Children.Add(_text);

            _status = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap, FontSize = 13, MinHeight = 24,
                Foreground = ThemeBrushes.TextPrimary, Margin = new Thickness(0, 8, 0, 8),
            };
            System.Windows.Automation.AutomationProperties.SetLiveSetting(_status, System.Windows.Automation.AutomationLiveSetting.Assertive);
            Grid.SetRow(_status, 2);
            grid.Children.Add(_status);

            var row = new WrapPanel();
            _btnStart = Btn(_sr ? "Start" : "Start", StartLog);
            _btnStop = Btn(_sr ? "Stop" : "Stop", StopLog);
            _btnClear = Btn(_sr ? "Obriši prikaz" : "Clear", ClearLog);
            _btnSave = Btn(_sr ? "Sačuvaj..." : "Save...", SaveLog);
            var close = Btn(_sr ? "Zatvori" : "Close", Close);
            close.IsCancel = true;
            row.Children.Add(_btnStart);
            row.Children.Add(_btnStop);
            row.Children.Add(_btnClear);
            row.Children.Add(_btnSave);
            row.Children.Add(close);
            Grid.SetRow(row, 3);
            grid.Children.Add(row);

            Content = grid;
            KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
            Closed += (s, e) => { try { _cts?.Cancel(); } catch { } };
            UpdateButtons();
            Loaded += (s, e) => StartLog();     // odmah kreni
            UiScaling.Register(this);
        }

        private Button Btn(string content, Action onClick)
        {
            var b = new Button
            {
                Content = content, Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(0, 0, 8, 8), Cursor = Cursors.Hand,
            };
            b.Click += (s, e) => onClick();
            return b;
        }

        private void UpdateButtons()
        {
            _btnStart.IsEnabled = !_running;
            _btnStop.IsEnabled = _running;
        }

        private void Announce(string text) => Announcer.Say(_status, text);

        private void StartLog()
        {
            if (_running) return;
            _cts = new CancellationTokenSource();
            _running = true;
            UpdateButtons();
            Announce(_sr ? "Log pokrenut." : "Log started.");

            // Ispis na gomilu na svakih 300 ms — UI nit ostaje slobodna.
            _flushTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _flushTimer.Tick += (s, e) => Flush();
            _flushTimer.Start();

            _ = RunLoopAsync(_cts.Token);
        }

        private async System.Threading.Tasks.Task RunLoopAsync(CancellationToken ct)
        {
            try
            {
                await DeviceEngine.StartSyslogAsync(_udid, OnLine, ct);
            }
            catch (OperationCanceledException) { /* Stop */ }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => Announce((_sr ? "Greška: " : "Error: ") + ex.Message));
            }
            finally
            {
                Dispatcher.Invoke(() =>
                {
                    _flushTimer?.Stop();
                    _flushTimer = null;
                    Flush();                 // ispiši ostatak
                    _running = false;
                    UpdateButtons();
                    if (!ct.IsCancellationRequested) Announce(_sr ? "Log je prekinut." : "Log stopped.");
                });
            }
        }

        // Dolazi sa POZADINSKE niti vrlo često — samo ubaci u bafer (jeftino, bez UI-ja).
        private void OnLine(string line)
        {
            if (line == null) return;
            lock (_lock)
            {
                _pending.Append(line).Append('\n');
                // ako UI ne stigne da flush-uje, ne daj baferu da raste bez granice
                if (_pending.Length > MaxViewChars * 2)
                    _pending.Remove(0, _pending.Length - MaxViewChars);
            }
        }

        // Teče na UI niti, na tajmer: uzmi sve nakupljeno i ispiši odjednom.
        private void Flush()
        {
            string chunk;
            lock (_lock)
            {
                if (_pending.Length == 0) return;
                chunk = _pending.ToString();
                _pending.Clear();
            }

            _buffer.Append(chunk);
            if (_buffer.Length > MaxBufferChars)
                _buffer.Remove(0, _buffer.Length - MaxBufferChars);

            _text.AppendText(chunk);
            if (_text.Text.Length > MaxViewChars)
                _text.Text = _text.Text.Substring(_text.Text.Length - MaxViewChars);
            _text.ScrollToEnd();
        }

        private void StopLog()
        {
            try { _cts?.Cancel(); } catch { }
            Announce(_sr ? "Zaustavljam log." : "Stopping log.");
        }

        private void ClearLog()
        {
            _text.Clear();
            _buffer.Clear();
            lock (_lock) { _pending.Clear(); }
            Announce(_sr ? "Prikaz obrisan." : "Cleared.");
        }

        private void SaveLog()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = _sr ? "Sačuvaj log" : "Save log",
                FileName = "device-log.txt",
                Filter = (_sr ? "Tekst" : "Text") + " (*.txt)|*.txt|" + (_sr ? "Svi fajlovi" : "All files") + " (*.*)|*.*",
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                System.IO.File.WriteAllText(dlg.FileName, _buffer.ToString());
                Announce(_sr ? "Log sačuvan." : "Log saved.");
            }
            catch (Exception ex) { Announce((_sr ? "Greška: " : "Error: ") + ex.Message); }
        }
    }
}
