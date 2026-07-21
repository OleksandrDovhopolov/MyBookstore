namespace Book.Sell.Domain
{
    /// <summary>
    /// One authored passive purchase attempt attached to a spawned customer.
    /// </summary>
    public sealed class ScriptedPassiveAttempt
    {
        public string Genre { get; }
        public bool ForceHit { get; }

        public ScriptedPassiveAttempt(string genre, bool forceHit)
        {
            Genre = genre;
            ForceHit = forceHit;
        }
    }
}
