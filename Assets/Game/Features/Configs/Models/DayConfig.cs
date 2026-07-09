namespace Game.Configs.Models
{
    [ConfigFile("days")]
    public sealed class DayConfig : IConfig
    {
        public string Id { get; set; }
        public int DayIndex { get; set; }
    }
}
