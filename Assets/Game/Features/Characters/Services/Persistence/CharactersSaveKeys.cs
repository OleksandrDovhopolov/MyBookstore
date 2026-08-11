namespace Game.Characters.Services.Persistence
{
    /// <summary>Save module keys owned by the Characters feature.</summary>
    public static class CharactersSaveKeys
    {
        public const string State = "characters";

        // v2: adds flat SeenMemoryIds for Journal notification tracking.
        public const int StateSchemaVersion = 2;
    }
}
