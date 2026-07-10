using UnityEngine;

namespace Infrastructure.ResourceAnimations
{
    public static class ResourceAnimationRequestRules
    {
        public static int ResolveParticleCount(int amount, int maxParticles)
        {
            if (amount == 0) return 0;
            return Mathf.Clamp(Mathf.Abs(amount), 1, Mathf.Max(1, maxParticles));
        }

        public static string ResolveSpriteId(ResourceAnimationRequest request) =>
            string.IsNullOrWhiteSpace(request.SpriteId) ? request.ResourceId : request.SpriteId;
    }
}
