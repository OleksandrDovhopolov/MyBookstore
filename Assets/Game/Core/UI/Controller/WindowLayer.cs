namespace Game.UI
{
    public enum WindowLayer
    {
        // Always at the bottom. Used by WindowType.HUD. Intentionally excluded from the
        // focus chain in UIStack.FocusOrder — HideTopAsync() must never close HUD windows.
        Hud,
        Main,
        Additional,
        System,
        Develop,
    }
}
