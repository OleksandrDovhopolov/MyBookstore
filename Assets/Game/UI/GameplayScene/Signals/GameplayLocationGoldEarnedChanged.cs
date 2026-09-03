namespace Game.UI
{
    public readonly struct GameplayLocationGoldEarnedChanged
    {
        public int Amount { get; }

        public GameplayLocationGoldEarnedChanged(int amount)
        {
            Amount = amount;
        }
    }
}
