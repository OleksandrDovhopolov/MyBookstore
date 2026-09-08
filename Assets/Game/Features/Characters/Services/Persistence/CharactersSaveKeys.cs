namespace Game.Characters.Services.Persistence
{
    /// <summary>Save module keys owned by the Characters feature.</summary>
    public static class CharactersSaveKeys
    {
        public const string State = "characters";

        // Release baseline: all pre-release saves are wiped, so the public schema starts at v1.
        public const int StateSchemaVersion = 1;
    }
}
