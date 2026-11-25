using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace UltraIDevice
{
    /// <summary>
    /// Pristupačan čitač izvezenih crash logova. Lista fajlove iz foldera (i podfoldera)
    /// i prikazuje sadržaj izabranog. Binarne .plist fajlove automatski pretvara u
    /// čitljiv XML (preko plistutil-a); .ips/.crash/.log/.txt prikazuje kao tekst.
    /// </summary>
    public sealed class CrashLogWindow : Window
    {
        private readonly bool _sr = LanguageManager.Current == "sr";
        private string _folder;
        private bool _busy;

        private readonly TextBlock _folderLabel;
        private readonly TextBlock _status;
        private readonly ListBox _list;
        private readonly TextBox _text;

        public CrashLogWindow(Window owner, string folder)
        {
            _folder = folder ?? "";
            Owner = owner;
            Title = _sr ? "Čitač crash logova" : "Crash log reader";
            Width = 820; Height = 640;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = ThemeBrushes.MainWindowBg;
            Foreground = ThemeBrushes.TextPrimary;

            var grid = new Grid { Margin = new Thickness(16) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // heading
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // folder
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // list
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) }); // text
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // status
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // buttons

            var heading = new TextBlock
            {
                Text = _sr ? "Čitač crash logova" : "Crash log reader",
                FontSize = 17, FontWeight = FontWeights.Bold,
                Foreground = ThemeBrushes.TextPrimary, Margin = new Thickness(0, 0, 0, 8),
            };
            System.Windows.Automation.AutomationProperties.SetHeadingLevel(heading, System.Windows.Automation.AutomationHeadingLevel.Level1);
            Grid.SetRow(heading, 0);
            grid.Children.Add(heading);

            _folderLabel = new TextBlock
            {
                Text = _folder, TextWrapping = TextWrapping.Wrap, FontSize = 12,
                Foreground = ThemeBrushes.TextSecondary, Margin = new Thickness(0, 0, 0, 8),
            };
            System.Windows.Automation.AutomationProperties.SetName(_folderLabel, _sr ? "Folder" : "Folder");
            Grid.SetRow(_folderLabel, 1);
            grid.Children.Add(_folderLabel);

            _list = new ListBox
            {
                Background = ThemeBrushes.ControlBg, Foreground = ThemeBrushes.TextPrimary,
                BorderBrush = ThemeBrushes.ThemeBorderBrush, FontSize = 13,
                Margin = new Thickness(0, 0, 0, 8),
            };
            System.Windows.Automation.AutomationProperties.SetName(_list, _sr ? "Fajlovi crash logova" : "Crash log files");
            _list.SelectionChanged += async (s, e) => await ShowSelectedAsync();
            _list.KeyDown += (s, e) => { if (e.Key == Key.F5) { e.Handled = true; LoadList(); } };
            Grid.SetRow(_list, 2);
            grid.Children.Add(_list);

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
            System.Windows.Automation.AutomationProperties.SetName(_text, _sr ? "Sadržaj izveštaja" : "Report contents");
            Grid.SetRow(_text, 3);
            grid.Children.Add(_text);

            _status = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap, FontSize = 13, MinHeight = 24,
                Foreground = ThemeBrushes.TextPrimary, Margin = new Thickness(0, 8, 0, 8),
            };
            System.Windows.Automation.AutomationProperties.SetLiveSetting(_status, System.Windows.Automation.AutomationLiveSetting.Assertive);
            Grid.SetRow(_status, 4);
            grid.Children.Add(_status);

            var row = new WrapPanel();
            row.Children.Add(Btn(_sr ? "Otvori drugi folder..." : "Open another folder...", OpenFolder));
            row.Children.Add(Btn(_sr ? "Osveži" : "Refresh", LoadList));
            row.Children.Add(Btn(_sr ? "Sačuvaj kao tekst..." : "Save as text...", SaveAsText));
            var close = Btn(_sr ? "Zatvori" : "Close", Close);
            close.IsCancel = true;
            row.Children.Add(close);
            Grid.SetRow(row, 5);
            grid.Children.Add(row);

            Content = grid;
            KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
            Loaded += (s, e) => { _list.Focus(); LoadList(); };
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

        private void Announce(string text) => Announcer.Say(_status, text);

        private void LoadList()
        {
            _list.Items.Clear();
            _folderLabel.Text = _folder;
            if (string.IsNullOrEmpty(_folder) || !Directory.Exists(_folder))
            {
                Announce(_sr ? "Folder ne postoji." : "Folder does not exist.");
                return;
            }
            try
            {
                var files = Directory.EnumerateFiles(_folder, "*", SearchOption.AllDirectories).Take(5000).ToList();
                // Najnoviji izveštaji na vrhu.
                files.Sort((a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
                foreach (var f in files)
                {
                    string rel = f.Length > _folder.Length ? f.Substring(_folder.Length).TrimStart('\\', '/') : Path.GetFileName(f);
                    _list.Items.Add(new ListBoxItem { Content = rel, Tag = f, Foreground = ThemeBrushes.TextPrimary });
                }
                Announce(string.Format(_sr ? "{0} fajlova. Strelicama biraj, sadržaj se prikazuje ispod." : "{0} files. Use arrows to pick; contents show below.", files.Count));
                if (_list.Items.Count > 0) _list.SelectedIndex = 0;
            }
            catch (Exception ex) { Announce((_sr ? "Greška: " : "Error: ") + ex.Message); }
        }

        private async System.Threading.Tasks.Task ShowSelectedAsync()
        {
            if (_busy) return;
            if (_list.SelectedItem is not ListBoxItem it || it.Tag is not string path) return;
            _busy = true;
            try
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                string content;
                if (ext == ".plist")
                {
                    string xml = await DeviceEngine.ConvertPlistAsync(path, System.Threading.CancellationToken.None);
                    content = xml ?? SafeReadText(path);   // XML plist se i sirov lepo čita
                }
                else
                {
                    content = SafeReadText(path);
                }

                if (content.Length > 500000)
                    content = content.Substring(0, 500000) + "\n\n..." + (_sr ? "(skraćeno)" : "(truncated)");
                _text.Text = content;
                _text.CaretIndex = 0;
                Announce(_sr ? "Prikazano." : "Shown.");
            }
            catch (Exception ex) { Announce((_sr ? "Greška: " : "Error: ") + ex.Message); }
            finally { _busy = false; }
        }

        private static string SafeReadText(string path)
        {
            try { return File.ReadAllText(path); }
            catch
            {
                // Binarni sadržaj bez plistutil-a — bar nešto čitljivo, bez pucanja.
                try { return System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(path)); }
                catch { return ""; }
            }
        }

        private void OpenFolder()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = _sr ? "Izaberi folder sa logovima" : "Choose a logs folder" };
            if (dlg.ShowDialog(this) != true) return;
            _folder = dlg.FolderName;
            LoadList();
        }

        private void SaveAsText()
        {
            if (string.IsNullOrEmpty(_text.Text)) { Announce(_sr ? "Nema šta da se sačuva." : "Nothing to save."); return; }
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = _sr ? "Sačuvaj kao tekst" : "Save as text",
                FileName = "crash-report.txt",
                Filter = (_sr ? "Tekst" : "Text") + " (*.txt)|*.txt|" + (_sr ? "Svi fajlovi" : "All files") + " (*.*)|*.*",
            };
            if (dlg.ShowDialog(this) != true) return;
            try { File.WriteAllText(dlg.FileName, _text.Text); Announce(_sr ? "Sačuvano." : "Saved."); }
            catch (Exception ex) { Announce((_sr ? "Greška: " : "Error: ") + ex.Message); }
        }
    }
}
