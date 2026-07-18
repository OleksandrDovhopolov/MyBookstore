namespace Game.UI
{
    public readonly struct SalesPauseRequested
    {
        public bool Paused { get; }

        public SalesPauseRequested(bool paused)
        {
            Paused = paused;
        }
    }
}
