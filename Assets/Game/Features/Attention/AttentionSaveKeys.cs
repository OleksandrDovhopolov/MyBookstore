namespace Game.Attention
{
    /// <summary>Save module owned by the shared attention feature.</summary>
    public static class AttentionSaveKeys
    {
        public const string State = "attention";

        // Release baseline: pre-release saves are wiped (RELEASE_TASKS INF-6), so the public schema
        // starts at v1. The predecessor module "journal.attention" is abandoned, not migrated — an
        // orphaned module round-trips inertly through SaveData.Modules.
        public const int StateSchemaVersion = 1;
    }
}
