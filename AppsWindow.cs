using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace UltraIDevice
{
    /// <summary>
    /// Pristupačan spisak aplikacija na uređaju (preko ideviceinstaller-a):
    /// lista sa strelicama, pa dugmad Instaliraj .ipa, Deinstaliraj, Osveži.
    /// Svaka radnja ima status koji čitač ekrana pročita bez pomeranja fokusa.
    /// </summary>
    public sealed class AppsWindow : Window
    {
        private readonly bool _sr = LanguageManager.Current == "sr";
        private readonly string _udid;
        private bool _busy;

        private readonly TextBlock _status;   // assertive live region
        private readonly ListBox _list;
        private readonly List<Button> _actionButtons = new();

        public AppsWindow(Window owner, DeviceInfo device)
        {
            _udid = device?.Udid ?? "";
            Owner = owner;
            Title = _sr ? "Aplikacije na uređaju" : "Apps on device";
            Width = 640; Height = 600;
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
                Text = _sr ? "Aplikacije na uređaju" : "Apps on device",
                FontSize = 17, FontWeight = FontWeights.Bold,
                Foreground = ThemeBrushes.TextPrimary, Margin = new Thickness(0, 0, 0, 8),
            };
            System.Windows.Automation.AutomationProperties.SetHeadingLevel(heading, System.Windows.Automation.AutomationHeadingLevel.Level1);
            Grid.SetRow(heading, 0);
            grid.Children.Add(heading);

            _list = new ListBox
            {
                Background = ThemeBrushes.ControlBg, Foreground = ThemeBrushes.TextPrimary,
                BorderBrush = ThemeBrushes.ThemeBorderBrush, FontSize = 14,
            };
            System.Windows.Automation.AutomationProperties.SetName(_list, _sr ? "Instalirane aplikacije" : "Installed apps");
            _list.KeyDown += (s, e) => { if (e.Key == Key.Delete) { e.Handled = true; _ = UninstallAsync(); } else if (e.Key == Key.F5) { e.Handled = true; _ = RefreshAsync(); } };
            Grid.SetRow(_list, 1);
            grid.Children.Add(_list);

            _status = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap, FontSize = 13, MinHeight = 24,
                Foreground = ThemeBrushes.TextPrimary, Margin = new Thickness(0, 8, 0, 8),
            };
            System.Windows.Automation.AutomationProperties.SetLiveSetting(_status, System.Windows.Automation.AutomationLiveSetting.Assertive);
            Grid.SetRow(_status, 2);
            grid.Children.Add(_status);

            var row = new WrapPanel();
            row.Children.Add(Act(_sr ? "Instaliraj .ipa" : "Install .ipa", () => InstallAsync()));
            row.Children.Add(Act(_sr ? "Deinstaliraj" : "Uninstall", () => UninstallAsync()));
            row.Children.Add(Act(_sr ? "Osveži" : "Refresh", () => RefreshAsync()));
            var close = new Button
            {
                Content = _sr ? "Zatvori" : "Close",
                Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 0, 8, 8), IsCancel = true,
            };
            close.Click += (s, e) => Close();
            row.Children.Add(close);
            Grid.SetRow(row, 3);
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

        private void Announce(string text) => Announcer.Say(_status, text);

        private void SetBusy(bool busy)
        {
            _busy = busy;
            foreach (var b in _actionButtons) b.IsEnabled = !busy;
        }

        private DeviceEngine.InstalledApp Selected =>
            (_list.SelectedItem as ListBoxItem)?.Tag as DeviceEngine.InstalledApp;

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            if (_busy) return;
            SetBusy(true);
            Announce(_sr ? "Učitavam listu aplikacija..." : "Loading app list...");
            try
            {
                var apps = await DeviceEngine.ListAppsAsync(_udid, System.Threading.CancellationToken.None);
                _list.Items.Clear();
                foreach (var a in apps)
                {
                    string label = string.IsNullOrEmpty(a.Version) ? a.Name : a.Name + "  —  " + a.Version;
                    label += "  (" + a.BundleId + ")";
                    _list.Items.Add(new ListBoxItem { Content = label, Tag = a, Foreground = ThemeBrushes.TextPrimary });
                }
                if (_list.Items.Count > 0) _list.SelectedIndex = 0;
                Announce(string.Format(_sr ? "{0} aplikacija." : "{0} apps.", apps.Count));
            }
            catch (Exception ex) { Announce((_sr ? "Greška: " : "Error: ") + ex.Message); }
            finally { SetBusy(false); }
        }

        private async System.Threading.Tasks.Task InstallAsync()
        {
            if (_busy) return;
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = _sr ? "Izaberi .ipa za instalaciju" : "Choose an .ipa to install",
                Filter = (_sr ? "iOS aplikacije" : "iOS apps") + " (*.ipa)|*.ipa|" + (_sr ? "Svi fajlovi" : "All files") + " (*.*)|*.*",
            };
            if (dlg.ShowDialog(this) != true) return;

            SetBusy(true);
            Announce(_sr ? "Instaliram aplikaciju..." : "Installing app...");
            try
            {
                await DeviceEngine.InstallAppAsync(_udid, dlg.FileName, System.Threading.CancellationToken.None);
                Announce(_sr ? "Aplikacija instalirana." : "App installed.");
                await RefreshCoreAsync();
            }
            catch (Exception ex) { Announce((_sr ? "Greška: " : "Error: ") + ex.Message); }
            finally { SetBusy(false); }
        }

        private async System.Threading.Tasks.Task UninstallAsync()
        {
            if (_busy) return;
            var a = Selected;
            if (a == null) return;
            if (!Dialogs.Confirm(this, _sr ? "Deinstalacija" : "Uninstall",
                string.Format(_sr ? "Deinstalirati \"{0}\"?" : "Uninstall \"{0}\"?", a.Name)))
                return;

            SetBusy(true);
            Announce(_sr ? "Deinstaliram..." : "Uninstalling...");
            try
            {
                await DeviceEngine.UninstallAppAsync(_udid, a.BundleId, System.Threading.CancellationToken.None);
                Announce(_sr ? "Deinstalirano." : "Uninstalled.");
                await RefreshCoreAsync();
            }
            catch (Exception ex) { Announce((_sr ? "Greška: " : "Error: ") + ex.Message); }
            finally { SetBusy(false); }
        }

        // Ponovo učita listu bez menjanja busy stanja i bez dodatne najave broja.
        private async System.Threading.Tasks.Task RefreshCoreAsync()
        {
            try
            {
                var apps = await DeviceEngine.ListAppsAsync(_udid, System.Threading.CancellationToken.None);
                _list.Items.Clear();
                foreach (var a in apps)
                {
                    string label = string.IsNullOrEmpty(a.Version) ? a.Name : a.Name + "  —  " + a.Version;
                    label += "  (" + a.BundleId + ")";
                    _list.Items.Add(new ListBoxItem { Content = label, Tag = a, Foreground = ThemeBrushes.TextPrimary });
                }
                if (_list.Items.Count > 0) _list.SelectedIndex = 0;
            }
            catch { /* najava je već data iz pozivaoca */ }
        }
    }
}
