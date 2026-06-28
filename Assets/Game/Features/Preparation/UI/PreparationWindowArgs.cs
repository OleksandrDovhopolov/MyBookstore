using Game.UI;

namespace Game.Preparation.UI
{
    /// <summary>
    /// Arguments for opening <see cref="PreparationWindow"/>. Carries the selected location so the
    /// window can show a human-readable name. Currently only one location exists; in the future the
    /// chosen location will be passed here. When no args are supplied the window falls back to the
    /// location stored in the preparation session state.
    /// </summary>
    public sealed class PreparationWindowArgs : WindowArgs
    {
        public string LocationId { get; }
        public string DisplayName { get; }

        public PreparationWindowArgs(string locationId, string displayName)
        {
            LocationId = locationId;
            DisplayName = displayName;
        }
    }
}
