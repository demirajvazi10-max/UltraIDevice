using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace UltraIDevice
{
    /// <summary>
    /// Prozor "Kako podržati projekat" — isti sadržaj i način kao u ostalim Ultra
    /// aplikacijama: Ko-fi, GitHub Sponsors i domaća uplata. Kod-om građen prozor
    /// (bez XAML-a), tematizovan i pristupačan.
    /// </summary>
    public static class SupportWindow
    {
        private const string KofiUrl = "https://ko-fi.com/ultracreativesuite";
        private const string SponsorsUrl = "https://github.com/sponsors/demirajvazi10-max";
        private const string BankAccount = "265-0000006185877-28";

        public static void Show(Window owner)
        {
            bool sr = LanguageManager.Current == "sr";

            var win = new Window
            {
                Title = sr ? "Kako podržati projekat" : "How to support this project",
                Owner = owner,
                Width = 600,
                Height = 520,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = ThemeBrushes.MainWindowBg,
                Foreground = ThemeBrushes.TextPrimary,
            };

            var panel = new StackPanel { Margin = new Thickness(18) };

            var heading = new TextBlock
            {
                Text = sr ? "Kako podržati projekat" : "How to support this project",
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeBrushes.TextPrimary,
                Margin = new Thickness(0, 0, 0, 10),
            };
            System.Windows.Automation.AutomationProperties.SetHeadingLevel(heading, System.Windows.Automation.AutomationHeadingLevel.Level1);
            panel.Children.Add(heading);

            string body = sr
                ? "Ultra iDevice je besplatan i ostaje besplatan. Ako želiš da podržiš razvoj:\n\n" +
                  "• Karticom preko Ko-fi — bez naloga: ukucaš iznos, pritisneš Donate i platiš Visa ili Mastercard karticom (ili PayPal-om). Najlakše iz inostranstva.\n\n" +
                  "• GitHub Sponsors — mesečno ili jednokratno, običnom platnom karticom (ne treba PayPal); treba besplatan GitHub nalog.\n\n" +
                  "• Uplata iz Srbije — primalac Demir Ajvazi, račun " + BankAccount + ", Raiffeisen banka, svrha: Donacija za Ultra programe. Za domaće uplate; iz inostranstva Ko-fi ima daleko manje troškove."
                : "Ultra iDevice is free and will stay free. If you'd like to support its development:\n\n" +
                  "• By card on Ko-fi — no account needed: type an amount, press Donate and pay with a Visa or Mastercard (or PayPal). The easiest way from abroad.\n\n" +
                  "• GitHub Sponsors — monthly or one-time, paid with an ordinary payment card (no PayPal needed); needs a free GitHub account.\n\n" +
                  "• Bank transfer from Serbia — recipient Demir Ajvazi, account " + BankAccount + ", Raiffeisen banka, purpose: Donacija za Ultra programe. Meant for domestic transfers; from abroad, Ko-fi costs far less in fees.";

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
                Height = 260,
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 12),
            };
            panel.Children.Add(text);

            var row = new WrapPanel();
            row.Children.Add(MakeButton(sr ? "Otvori Ko-fi" : "Open Ko-fi", () => OpenUrl(KofiUrl)));
            row.Children.Add(MakeButton(sr ? "Otvori GitHub Sponsors" : "Open GitHub Sponsors", () => OpenUrl(SponsorsUrl)));
            row.Children.Add(MakeButton(sr ? "Kopiraj broj računa" : "Copy account number", () =>
            {
                try { System.Windows.Clipboard.SetText(BankAccount); } catch { }
            }));
            panel.Children.Add(row);

            var close = MakeButton(sr ? "Zatvori" : "Close", () => win.Close());
            close.HorizontalAlignment = HorizontalAlignment.Right;
            close.Margin = new Thickness(0, 14, 0, 0);
            close.IsCancel = true;
            panel.Children.Add(close);

            win.Content = panel;
            win.KeyDown += (s, e) => { if (e.Key == Key.Escape) win.Close(); };
            UiScaling.Register(win);
            win.ShowDialog();
        }

        private static Button MakeButton(string content, Action onClick)
        {
            var b = new Button
            {
                Content = content,
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(0, 0, 8, 8),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            b.Click += (s, e) => onClick();
            return b;
        }

        private static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }
    }
}
