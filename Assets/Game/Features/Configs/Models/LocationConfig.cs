namespace Game.Configs.Models
{
    [ConfigFile("locations")]
    public sealed class LocationConfig : IConfig
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }

        public int EntryCost { get; set; }

        public string EntryCurrencyId { get; set; }

        public string LocationAddress { get; set; }

        public Newtonsoft.Json.Linq.JObject Unlock { get; set; }

        public string[] DemandGenres { get; set; }

        public string[] DemandTags { get; set; }

        /// <summary>
        /// Additive percent modifier this location applies to the day's regular customer count.
        /// Neutral = 0 (e.g. +0.20 = +20% visitors). Consumed by the customer traffic resolver
        /// (see docs/INPROGRESS/CUSTOMER_TRAFFIC_COUNT_SYSTEM.md).
        /// </summary>
        public float CustomerTrafficPercentDelta { get; set; }
    }
}
