namespace Game.Bootstrap.Loading
{
    /// <summary>
    /// Marker for a loading operation that blocks on a human decision (e.g. the first-run privacy gate).
    /// <see cref="LoadingOrchestrator"/> suspends the global loading deadline while such an operation runs,
    /// so a player who takes a minute to read — or leaves the app to open a link — does not get the
    /// generic "check your internet connection" retry screen.
    ///
    /// Implementors MUST have <see cref="ILoadingOperation.Timeout"/> == null: a per-operation timeout
    /// would cancel the dialog out from under the player and defeat the whole point.
    /// </summary>
    public interface IInteractiveLoadingOperation
    {
    }
}
