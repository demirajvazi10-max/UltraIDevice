using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace UltraIDevice
{
    /// <summary>
    /// Pristupačan menadžer fajlova na uređaju (preko AFC-a). Namenjen i slepom i
    /// videćem korisniku: lista stavki sa strelicama, Enter ulazi u fasciklu,
    /// Backspace vraća gore, a sve radnje (Preuzmi, Dodaj, Obriši, Nova fascikla)
    /// imaju imena i status koji čitač ekrana pročita bez pomeranja fokusa.
    ///
    /// Vidljiva je medijska particija (DCIM/fotografije, Downloads, Books...). Bez
    /// jailbreak-a ne vidi se ceo sistem — to je Apple ograničenje, ne aplikacije.
    /// </summary>
    public sealed class FileManagerWindow : Window
    {
        private readonly bool _sr = LanguageManager.Current == "sr";
        private readonly string _udid;
        private string _path = "/";
        private bool _busy;

        private readonly TextBlock _pathLabel;
        private readonly TextBlock _status;      // assertive live region
        private readonly ListBox _list;
        private readonly List<Button> _actionButtons = new();

        public FileManagerWindow(Window owner, DeviceInfo device)
        {
            _udid = device?.Udid ?? "";
            Owner = owner;
            Title = _sr ? "Fajlovi na uređaju" : "Device files";
            Width = 640; Height = 620;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = ThemeBrushes.MainWindowBg;
            Foreground = ThemeBrushes.TextPrimary;

            var grid = new Grid { Margin = new Thickness(16) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // naslov
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // putanja
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // lista
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // status
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // dugmad

            var heading = new TextBlock
            {
                Text = _sr ? "Fajlovi na uređaju" : "Device files",
                FontSize = 17, FontWeight = FontWeights.Bold,
                Foreground = ThemeBrushes.TextPrimary, Margin = new Thickness(0, 0, 0, 8),
            };
            System.Windows.Automation.AutomationProperties.SetHeadingLevel(heading, System.Windows.Automation.AutomationHeadingLevel.Level1);
            Grid.SetRow(heading, 0);
            grid.Children.Add(heading);

            _pathLabel = new TextBlock
            {
                Text = _path, TextWrapping = TextWrapping.Wrap, FontSize = 13,
                Foreground = ThemeBrushes.TextSecondary, Margin = new Thickness(0, 0, 0, 8),
            };
            System.Windows.Automation.AutomationProperties.SetName(_pathLabel, _sr ? "Trenutna fascikla" : "Current folder");
            System.Windows.Automation.AutomationProperties.SetLiveSetting(_pathLabel, System.Windows.Automation.AutomationLiveSetting.Polite);
            Grid.SetRow(_pathLabel, 1);
            grid.Children.Add(_pathLabel);

            _list = new ListBox
            {
                Background = ThemeBrushes.ControlBg, Foreground = ThemeBrushes.TextPrimary,
                BorderBrush = ThemeBrushes.ThemeBorderBrush, FontSize = 14,
            };
            System.Windows.Automation.AutomationProperties.SetName(_list, _sr ? "Fajlovi i fascikle" : "Files and folders");
            _list.KeyDown += List_KeyDown;
            _list.MouseDoubleClick += async (s, e) => await OpenSelectedAsync();
            Grid.SetRow(_list, 2);
            grid.Children.Add(_list);

            _status = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap, FontSize = 13, MinHeight = 24,
                Foreground = ThemeBrushes.TextPrimary, Margin = new Thickness(0, 8, 0, 8),
            };
            System.Windows.Automation.AutomationProperties.SetLiveSetting(_status, System.Windows.Automation.AutomationLiveSetting.Assertive);
            Grid.SetRow(_status, 3);
            grid.Children.Add(_status);

            var row = new WrapPanel();
            row.Children.Add(Act(_sr ? "Otvori" : "Open", () => OpenSelectedAsync()));
            row.Children.Add(Act(_sr ? "Nazad gore" : "Up", () => UpAsync()));
            row.Children.Add(Act(_sr ? "Preuzmi" : "Download", () => DownloadAsync()));
            row.Children.Add(Act(_sr ? "Dodaj fajl" : "Add file", () => AddFileAsync()));
            row.Children.Add(Act(_sr ? "Obriši" : "Delete", () => DeleteSelectedAsync()));
            row.Children.Add(Act(_sr ? "Nova fascikla" : "New folder", () => NewFolderAsync()));
            row.Children.Add(Act(_sr ? "Osveži" : "Refresh", () => RefreshAsync()));
            var close = new Button
            {
                Content = _sr ? "Zatvori" : "Close",
                Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 0, 8, 8), IsCancel = true,
            };
            close.Click += (s, e) => Close();
            row.Children.Add(close);
            Grid.SetRow(row, 4);
            grid.Children.Add(row);

            Content = grid;
            KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
            Loaded += async (s, e) => { _list.Focus(); await RefreshAsync(); };
            UiScaling.Register(this);
        }

        private Button Act(string content, Func<System.Threading.Tasks.Task> onClick)
        {
            var b = new Button
            {
                Content = content, Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(0, 0, 8, 8), Cursor = Cursors.Hand,
            };
            b.Click += async (s, e) => await onClick();
            _actionButtons.Add(b);
            return b;
        }

        private void List_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter: e.Handled = true; _ = OpenSelectedAsync(); break;
                case Key.Back: e.Handled = true; _ = UpAsync(); break;
                case Key.Delete: e.Handled = true; _ = DeleteSelectedAsync(); break;
                case Key.F5: e.Handled = true; _ = RefreshAsync(); break;
            }
        }

        private void Announce(string text) => Announcer.Say(_status, text);

        private void SetBusy(bool busy)
        {
            _busy = busy;
            foreach (var b in _actionButtons) b.IsEnabled = !busy;
        }

        private AfcEngine.AfcEntry Selected =>
            (_list.SelectedItem as ListBoxItem)?.Tag as AfcEngine.AfcEntry;

        private void Populate(List<AfcEngine.AfcEntry> items)
        {
            _list.Items.Clear();
            foreach (var e in items)
            {
                string kind =
                    e.IsDir == true ? (_sr ? "fascikla" : "folder") :
                    e.IsDir == false ? (e.Size >= 0 ? AfcEngine.HumanSize(e.Size) : (_sr ? "fajl" : "file")) :
                    (_sr ? "stavka" : "item");
                _list.Items.Add(new ListBoxItem
                {
                    Content = e.Name + "  —  " + kind,
                    Tag = e,
                    Foreground = ThemeBrushes.TextPrimary,
                });
            }
            _pathLabel.Text = _path;
            if (_list.Items.Count > 0) _list.SelectedIndex = 0;
        }

        /// <summary>Učita trenutnu fasciklu u listu (bez najave); vraća broj stavki.</summary>
        private async System.Threading.Tasks.Task<int> LoadCoreAsync()
        {
            var items = await AfcEngine.ListAsync(_udid, _path);
            Populate(items);
            return items.Count;
        }

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            if (_busy) return;
            SetBusy(true);
            Announce(_sr ? "Učitavam fasciklu..." : "Loading folder...");
            try
            {
                int n = await LoadCoreAsync();
                Announce(string.Format(_sr ? "{0} — {1} stavki." : "{0} — {1} items.", _path, n));
            }
            catch (Exception ex) { Announce((_sr ? "Ne mogu da otvorim: " : "Can't open: ") + ex.Message); }
            finally { SetBusy(false); }
        }

        private async System.Threading.Tasks.Task OpenSelectedAsync()
        {
            if (_busy) return;
            var e = Selected;
            if (e == null) return;
            if (e.IsDir == false)
            {
                Announce(_sr ? "To je fajl. Dugmetom Preuzmi ga sačuvaj na računar." : "That's a file. Use Download to save it to the PC.");
                return;
            }

            string target = AfcEngine.JoinPath(_path, e.Name);
            SetBusy(true);
            Announce(_sr ? "Otvaram..." : "Opening...");
            try
            {
                var items = await AfcEngine.ListAsync(_udid, target);
                _path = target;
                Populate(items);
                Announce(string.Format(_sr ? "{0} — {1} stavki." : "{0} — {1} items.", _path, items.Count));
            }
            catch (Exception ex)
            {
                if (e.IsDir == null)
                    Announce(_sr ? "Izgleda da je ovo fajl. Koristi Preuzmi." : "This looks like a file. Use Download.");
                else
                    Announce((_sr ? "Greška: " : "Error: ") + ex.Message);
            }
            finally { SetBusy(false); }
        }

        private async System.Threading.Tasks.Task UpAsync()
        {
            if (_busy) return;
            if (_path == "/" || string.IsNullOrEmpty(_path))
            {
                Announce(_sr ? "Već si u korenu." : "Already at the top.");
                return;
            }
            _path = AfcEngine.Parent(_path);
            await RefreshAsync();
        }

        private async System.Threading.Tasks.Task DownloadAsync()
        {
            if (_busy) return;
            var e = Selected;
            if (e == null) return;
            if (e.IsDir == true)
            {
                Announce(_sr ? "Izabrana je fascikla, ne fajl." : "That's a folder, not a file.");
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog { FileName = e.Name, Title = _sr ? "Sačuvaj kao" : "Save as" };
            if (dlg.ShowDialog(this) != true) return;

            string remote = AfcEngine.JoinPath(_path, e.Name);
            SetBusy(true);
            Announce(_sr ? "Preuzimam..." : "Downloading...");
            try
            {
                await AfcEngine.DownloadAsync(_udid, remote, dlg.FileName);
                Announce(_sr ? "Preuzeto." : "Downloaded.");
            }
            catch (Exception ex) { Announce((_sr ? "Greška: " : "Error: ") + ex.Message); }
            finally { SetBusy(false); }
        }

        private async System.Threading.Tasks.Task AddFileAsync()
        {
            if (_busy) return;
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = _sr ? "Izaberi fajl za slanje" : "Choose a file to send" };
            if (dlg.ShowDialog(this) != true) return;

            string local = dlg.FileName;
            string remote = AfcEngine.JoinPath(_path, System.IO.Path.GetFileName(local));
            SetBusy(true);
            Announce(_sr ? "Šaljem fajl..." : "Sending file...");
            try
            {
                await AfcEngine.UploadAsync(_udid, local, remote);
                await LoadCoreAsync();
                Announce(_sr ? "Fajl poslat." : "File sent.");
            }
            catch (Exception ex) { Announce((_sr ? "Greška: " : "Error: ") + ex.Message); }
            finally { SetBusy(false); }
        }

        private async System.Threading.Tasks.Task DeleteSelectedAsync()
        {
            if (_busy) return;
            var e = Selected;
            if (e == null) return;
            if (!Dialogs.Confirm(this, _sr ? "Brisanje" : "Delete",
                string.Format(_sr ? "Obrisati \"{0}\"? Ovo se ne može poništiti." : "Delete \"{0}\"? This cannot be undone.", e.Name)))
                return;

            string remote = AfcEngine.JoinPath(_path, e.Name);
            SetBusy(true);
            Announce(_sr ? "Brišem..." : "Deleting...");
            try
            {
                await AfcEngine.DeleteAsync(_udid, remote);
                await LoadCoreAsync();
                Announce(_sr ? "Obrisano." : "Deleted.");
            }
            catch (Exception ex) { Announce((_sr ? "Greška: " : "Error: ") + ex.Message); }
            finally { SetBusy(false); }
        }

        private async System.Threading.Tasks.Task NewFolderAsync()
        {
            if (_busy) return;
            string name = Dialogs.Prompt(this, _sr ? "Nova fascikla" : "New folder", _sr ? "Ime fascikle:" : "Folder name:");
            if (string.IsNullOrWhiteSpace(name)) return;

            string remote = AfcEngine.JoinPath(_path, name.Trim());
            SetBusy(true);
            Announce(_sr ? "Pravim fasciklu..." : "Creating folder...");
            try
            {
                await AfcEngine.MkdirAsync(_udid, remote);
                await LoadCoreAsync();
                Announce(_sr ? "Fascikla napravljena." : "Folder created.");
            }
            catch (Exception ex) { Announce((_sr ? "Greška: " : "Error: ") + ex.Message); }
            finally { SetBusy(false); }
        }
    }
}
