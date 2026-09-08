namespace Game.Configs.Models
{
    /// <summary>
    /// Authored customer visit scheduled either by day or by quest activation.
    /// </summary>
    [ConfigFile("customer_scripts")]
    public sealed class CustomerScriptConfig : IConfig
    {
        public string Id { get; set; }
        public int? DayIndex { get; set; }
        public string ActivationQuestId { get; set; }

        /// <summary>
        /// Optional location gate. When set, the script is eligible only while the current sales day runs at
        /// this location (matched against <c>SalesSessionSetup.LocationId</c>). Null/empty = any location.
        /// </summary>
        public string LocationId { get; set; }

        public string DialogueId { get; set; }
        public string CharacterId { get; set; }
        public bool ActiveRequest { get; set; }
        public ScriptedPassivePurchaseConfig[] PassiveAttempts { get; set; }
    }
}
