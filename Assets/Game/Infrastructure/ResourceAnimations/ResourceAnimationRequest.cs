using System;

namespace Infrastructure.ResourceAnimations
{
    public readonly struct ResourceAnimationRequest
    {
        public string ResourceId { get; }
        public int Amount { get; }
        public ResourceAnimationEndpoint From { get; }
        public ResourceAnimationEndpoint To { get; }
        public string SpriteId { get; }

        /// <summary>
        /// Invoked once per particle at the moment it lands on the destination endpoint.
        /// Opaque to the animation service (it never inspects counters) — callers use it to
        /// drive count-up on arrival. May fire multiple times per request; receivers must be
        /// idempotent.
        /// </summary>
        public Action OnParticleArrived { get; }

        public ResourceAnimationRequest(
            string resourceId,
            int amount,
            ResourceAnimationEndpoint from,
            ResourceAnimationEndpoint to,
            string spriteId = null,
            Action onParticleArrived = null)
        {
            ResourceId = resourceId;
            Amount = amount;
            From = from;
            To = to;
            SpriteId = spriteId;
            OnParticleArrived = onParticleArrived;
        }
    }
}
