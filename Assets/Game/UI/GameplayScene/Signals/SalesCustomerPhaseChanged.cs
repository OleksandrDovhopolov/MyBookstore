namespace Game.UI
{
    public readonly struct SalesCustomerPhaseChanged
    {
        public string CustomerId { get; }
        public string CharacterId { get; }
        public string Phase { get; }

        public SalesCustomerPhaseChanged(string customerId, string characterId, string phase)
        {
            CustomerId = customerId;
            CharacterId = characterId;
            Phase = phase;
        }
    }
}
