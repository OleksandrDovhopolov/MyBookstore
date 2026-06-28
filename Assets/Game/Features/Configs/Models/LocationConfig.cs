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
    }
}
