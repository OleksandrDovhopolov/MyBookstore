namespace Infrastructure.ResourceAnimations
{
    public readonly struct ResourceAnimationRequest
    {
        public string ResourceId { get; }
        public int Amount { get; }
        public ResourceAnimationEndpoint From { get; }
        public ResourceAnimationEndpoint To { get; }
        public string SpriteId { get; }

        public ResourceAnimationRequest(
            string resourceId,
            int amount,
            ResourceAnimationEndpoint from,
            ResourceAnimationEndpoint to,
            string spriteId = null)
        {
            ResourceId = resourceId;
            Amount = amount;
            From = from;
            To = to;
            SpriteId = spriteId;
        }
    }
}
