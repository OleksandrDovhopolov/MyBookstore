namespace Game.Characters.Services.Persistence
{
    /// <summary>Save module keys owned by the Characters feature.</summary>
    public static class CharactersSaveKeys
    {
        public const string State = "characters";

        // v1: discovered flag only. Memory unlock is derived from quest state, not persisted.
        public const int StateSchemaVersion = 1;
    }
}
