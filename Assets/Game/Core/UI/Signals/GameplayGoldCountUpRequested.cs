namespace Game.UI
{
    public readonly struct GameplayGoldCountUpRequested
    {
        public float DurationSeconds { get; }

        public GameplayGoldCountUpRequested(float durationSeconds)
        {
            DurationSeconds = durationSeconds;
        }
    }
}
