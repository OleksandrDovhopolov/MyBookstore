namespace Game.Ftue.Domain
{
    /// <summary>
    /// First-launch marker: the FTUE preset (starter gold + starter books) has been applied.
    /// Idempotency mirrors <c>results.applied_rewards</c>: on subsequent launches the FTUE service
    /// sees Applied=true and skips the seeding.
    /// POCO: serialized by Newtonsoft via ISaveService.UpdateModuleAsync under the
    /// <c>FtueSaveKeys.Applied</c> key.
    /// </summary>
    public sealed class FtueAppliedState
    {
        public bool Applied { get; set; }

        /// <summary>ISO-8601 UTC timestamp of when the preset was applied. Audit-only; nobody reads it.</summary>
        public string AppliedAtUtcIso { get; set; }
    }
}
