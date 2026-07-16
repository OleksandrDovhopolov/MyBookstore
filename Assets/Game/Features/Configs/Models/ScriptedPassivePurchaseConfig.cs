namespace Game.Configs.Models
{
    /// <summary>
    /// One authored passive purchase attempt for a story/customer character.
    /// </summary>
    public sealed class ScriptedPassivePurchaseConfig
    {
        public string Genre { get; set; }
        public bool ForceHit { get; set; }
    }
}
