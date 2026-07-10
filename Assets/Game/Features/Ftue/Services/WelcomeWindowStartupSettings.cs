namespace Game.Ftue.Services
{
    /// <summary>Bootstrap-controlled presentation switch for the first-entry welcome window.</summary>
    public sealed class WelcomeWindowStartupSettings
    {
        public WelcomeWindowStartupSettings(bool startWelcomeWindow)
        {
            StartWelcomeWindow = startWelcomeWindow;
        }

        public bool StartWelcomeWindow { get; }
    }
}
