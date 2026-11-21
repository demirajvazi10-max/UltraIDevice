using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace UltraIDevice
{
    /// <summary>
    /// Mali pomoćni dijalozi. Potvrde idu kroz standardni MessageBox (potpuno
    /// pristupačan), a duži tekst (lista grešaka, prečice, About) ide kroz
    /// ShowText — tematizovan prozor sa read-only tekstom koji čitač ekrana može
    /// da čita red po red, i dugmetom Zatvori (Esc).
    /// </summary>
    public static class Dialogs
    {
        public static bool Confirm(Window owner, string title, string message)
        {
            var r = System.Windows.MessageBox.Show(owner, message, title,
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            return r == MessageBoxResult.Yes;
        }

        public static void Info(Window owner, string title, string message)
        {
            System.Windows.MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void Error(Window owner, string title, string message)
        {
            System.Windows.MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>Tematizovan prozor sa dužim, čitljivim tekstom.</summary>
        public static void ShowText(Window owner, string title, string body)
        {
            var win = new Window
            {
                Title = title,
                Owner = owner,
                Width = 620,
                Height = 540,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = ThemeBrushes.MainWindowBg,
                Foreground = ThemeBrushes.TextPrimary,
            };

            var grid = new Grid { Margin = new Thickness(16) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var heading = new TextBlock
            {
                Text = title,
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeBrushes.TextPrimary,
                Margin = new Thickness(0, 0, 0, 10),
            };
            System.Windows.Automation.AutomationProperties.SetHeadingLevel(heading, System.Windows.Automation.AutomationHeadingLevel.Level1);
            Grid.SetRow(heading, 0);
            grid.Children.Add(heading);

            var text = new TextBox
            {
                Text = body,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = ThemeBrushes.ControlBg,
                Foreground = ThemeBrushes.TextPrimary,
                BorderBrush = ThemeBrushes.ThemeBorderBrush,
                FontSize = 13,
                Padding = new Thickness(8),
            };
            Grid.SetRow(text, 1);
            grid.Children.Add(text);

            var closeBtn = new Button
            {
                Content = LanguageManager.Current == "sr" ? "Zatvori" : "Close",
                Padding = new Thickness(16, 6, 16, 6),
                Margin = new Thickness(0, 12, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                IsCancel = true,
                IsDefault = true,
            };
            closeBtn.Click += (s, e) => win.Close();
            Grid.SetRow(closeBtn, 2);
            grid.Children.Add(closeBtn);

            win.Content = grid;
            win.KeyDown += (s, e) => { if (e.Key == Key.Escape) win.Close(); };
            UiScaling.Register(win);
            win.ShowDialog();
        }
    }
}
