using Game.UI;

namespace Game.Location.UI
{
    /// <summary>
    /// Arguments for opening <see cref="LocationWindow"/>. Full-screen page; no payload yet — the window
    /// reads the location list from configs and unlock status from <c>ILocationUnlockService</c>.
    /// </summary>
    public sealed class LocationWindowArgs : WindowArgs
    {
        public LocationWindowArgs() => AsMain();
    }
}
