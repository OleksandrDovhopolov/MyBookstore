namespace Game.Configs.Models
{
    /// <summary>
    /// Questless authored customer visit scheduled by day.
    /// </summary>
    [ConfigFile("customer_scripts")]
    public sealed class CustomerScriptConfig : IConfig
    {
        public string Id { get; set; }
        public int DayIndex { get; set; }
        public string CharacterId { get; set; }
        public ScriptedPassivePurchaseConfig[] PassiveAttempts { get; set; }
    }
}
