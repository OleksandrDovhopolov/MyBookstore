namespace Game.Rewards.API
{
    /// <summary>
    /// Result of <see cref="IRewardGrantService.GrantAsync"/>. <see cref="Granted"/> reflects what was
    /// actually written to player state — for an expanded box this is the rolled contents, not the
    /// abstract spec the caller requested.
    /// </summary>
    public readonly struct RewardGrantResult
    {
        public bool Success { get; }
        public RewardSpec Granted { get; }
        public string FailureReason { get; }

        private RewardGrantResult(bool success, RewardSpec granted, string failureReason)
        {
            Success = success;
            Granted = granted;
            FailureReason = failureReason;
        }

        public static RewardGrantResult Ok(RewardSpec granted) =>
            new RewardGrantResult(true, granted, null);

        public static RewardGrantResult Fail(string reason) =>
            new RewardGrantResult(false, null, reason);
    }
}
