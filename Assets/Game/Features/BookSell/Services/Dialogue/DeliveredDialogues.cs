using System.Collections.Generic;

namespace Book.Sell.Services
{
    /// <summary>Save module payload for delivered (already-shown) dialogue ids (GAME-6 fire-once).</summary>
    public sealed class DeliveredDialogues
    {
        public List<string> Ids { get; set; }
    }

    /// <summary>Save module keys owned by the dialogue fire-once store. Mirrors <c>QuestsSaveKeys</c>.</summary>
    public static class DialoguesSaveKeys
    {
        public const string Delivered = "dialogues.delivered";
        public const int DeliveredSchemaVersion = 1;
    }
}
