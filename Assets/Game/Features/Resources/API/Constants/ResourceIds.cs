namespace Game.Resources.API
{
    /// <summary>
    /// Stable string ids for known resources. The id is the only thing that distinguishes one
    /// resource from another in <see cref="IResourcesService"/> — there is no per-type code path.
    /// Hard-currency (<see cref="Gems"/>) is here for forward-compat; adding new ids is a one-line
    /// change with no service-side impact.
    /// </summary>
    public static class ResourceIds
    {
        public const string Gold = "gold";
        public const string Gems = "gems";
    }
}
