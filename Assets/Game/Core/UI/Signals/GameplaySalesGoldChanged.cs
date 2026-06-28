namespace Game.UI
{
    public readonly struct GameplaySalesGoldChanged
    {
        public int GoldEarned { get; }
        public bool Visible { get; }

        public GameplaySalesGoldChanged(int goldEarned, bool visible)
        {
            GoldEarned = goldEarned;
            Visible = visible;
        }
    }
}
