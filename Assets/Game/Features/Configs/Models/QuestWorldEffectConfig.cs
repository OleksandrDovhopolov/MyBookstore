using Newtonsoft.Json.Linq;

namespace Game.Configs.Models
{
    /// <summary>
    /// A permanent world-state effect applied once when a quest reaches Awarded (docs/QUESTS.md §6), e.g.
    /// <c>locationCustomerBonus</c>, <c>unlockLocation</c>, <c>genrePriceMultiplier</c>. Dispatched by
    /// type to an effect handler in the Quest feature; <see cref="Params"/> carries handler-specific data.
    /// </summary>
    public sealed class QuestWorldEffectConfig
    {
        public string Type { get; set; }

        public JObject Params { get; set; }
    }
}
