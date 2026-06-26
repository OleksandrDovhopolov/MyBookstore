namespace Game.LocationUnlock.API
{
    /// <summary>Outcome of <see cref="ILocationUnlockService.TryUnlockAsync"/>.</summary>
    public enum UnlockResult
    {
        Ok,
        AlreadyUnlocked,
        ConditionsNotMet,
        NotEnoughCurrency,
        UnknownLocation
    }
}
