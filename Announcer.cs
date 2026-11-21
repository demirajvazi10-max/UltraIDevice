using System.Windows.Controls;
using System.Windows.Automation.Peers;

namespace UltraIDevice
{
    /// <summary>
    /// Najava teksta čitaču ekrana preko "live region"-a. Postavlja tekst na dati
    /// TextBlock i eksplicitno okida LiveRegionChanged, pa JAWS/NVDA pročitaju
    /// poruku bez pomeranja fokusa. Za videće korisnike tekst je i vidljiv.
    ///
    /// TextBlock treba da ima AutomationProperties.LiveSetting (Polite ili Assertive)
    /// postavljen u XAML-u ili ovde.
    /// </summary>
    public static class Announcer
    {
        public static void Say(TextBlock target, string text)
        {
            if (target == null) return;
            target.Text = text ?? "";
            try
            {
                var peer = UIElementAutomationPeer.FromElement(target)
                           ?? UIElementAutomationPeer.CreatePeerForElement(target);
                peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            }
            catch { /* automation nije kritičan — nikad ne ruši zbog najave */ }
        }
    }
}
