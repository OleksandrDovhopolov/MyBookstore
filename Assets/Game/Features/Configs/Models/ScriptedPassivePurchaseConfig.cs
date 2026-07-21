namespace Game.Configs.Models
{
    /// <summary>
    /// One authored passive purchase attempt for a quest customer.
    /// </summary>
    public sealed class ScriptedPassivePurchaseConfig
    {
        public string Genre { get; set; }
        public bool ForceHit { get; set; }
    }
}
