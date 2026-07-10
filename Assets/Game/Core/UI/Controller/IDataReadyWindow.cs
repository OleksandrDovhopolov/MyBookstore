namespace Game.UI
{
    /// <summary>
    /// A window that loads its own data asynchronously after being shown. <see cref="IsDataReady"/> flips
    /// to true once the window has finished loading everything it needs to display. Callers (e.g. the
    /// scene bootstrap waiting before revealing the screen) can poll this instead of guessing.
    /// </summary>
    public interface IDataReadyWindow
    {
        bool IsDataReady { get; }
    }
}
