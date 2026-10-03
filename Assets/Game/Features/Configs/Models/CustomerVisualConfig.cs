namespace Game.Configs.Models
{
    [ConfigFile("customer_visuals")]
    public sealed class CustomerVisualConfig : IConfig
    {
        public string Id { get; set; }
        public string FigureSpriteKey { get; set; }
        public string AvatarSpriteKey { get; set; }
        public float Weight { get; set; }
    }
}
