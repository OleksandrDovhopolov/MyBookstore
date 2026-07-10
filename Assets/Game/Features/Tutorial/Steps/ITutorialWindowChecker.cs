namespace Game.Tutorial.Steps
{
    /// <summary>
    /// Resolves a tutorials.json window id ("preparation", "location", …) to whether that window is shown.
    /// Implemented outside Game.Tutorial (in the bootstrap layer) so the engine never references the concrete
    /// window types — keeps Game.Tutorial decoupled from feature UI assemblies.
    /// </summary>
    public interface ITutorialWindowChecker
    {
        /// <summary>
        /// Returns false if <paramref name="windowId"/> is unknown (so the handler can warn + auto-advance
        /// instead of waiting forever); otherwise sets <paramref name="shown"/> to the current state.
        /// </summary>
        bool TryGetShown(string windowId, out bool shown);
    }
}
