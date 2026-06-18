namespace Game.UI
{
    public enum WindowType
    {
        Page,
        Popup,
        Widget,
        // Scene-unique HUD. Parented under UICanvasRoot.HudRoot, mapped to WindowLayer.Hud
        // (sortingOrder 0 — always below windows). Excluded from UIStack.FocusOrder so
        // HideTopAsync() cannot close it.
        HUD,
    }
}
