namespace Game.Rewards.API
{
    /// <summary>
    /// What kind of payload a <see cref="RewardItem"/> represents. Drives the dispatch in
    /// <c>LocalRewardGrantService</c>: Resource → IResourcesService, InventoryItem → IInventoryService.
    /// New kinds added here require a new dispatch branch (or a new server-side handler in Phase 1+).
    /// </summary>
    public enum RewardKind
    {
        Resource,
        InventoryItem
    }
}
