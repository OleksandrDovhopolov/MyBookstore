namespace Game.UI
{
    /// <summary>
    /// Published once the hub (GameplayScene HUD) is actually visible and actionable by the player —
    /// after the reveal and, on first entry, after the welcome window closes. Drives the tutorial's
    /// "hubReady" trigger. Deliberately later than hud.IsDataReady so a forced sequence never plays
    /// behind the transition cover / welcome letter.
    /// </summary>
    public readonly struct GameplayHubReady
    {
        public int Day { get; }

        public GameplayHubReady(int day) => Day = day;
    }
}
