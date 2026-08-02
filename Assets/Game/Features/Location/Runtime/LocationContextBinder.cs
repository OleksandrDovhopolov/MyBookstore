using System;
using System.Collections.Generic;
using Game.Location.API;
using UnityEngine;

namespace Game.Location.Runtime
{
    public sealed class LocationContextBinder : ILocationContext
    {
        private readonly ILocationContext _fallback;
        private ILocationContext _primary;

        public LocationContextBinder(ILocationContext fallback)
        {
            _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        }

        public void Bind(ILocationContext primary)
        {
            _primary = primary;
        }

        public bool IsBound => _primary?.IsBound ?? false;
        public string LocationId => !string.IsNullOrWhiteSpace(_primary?.LocationId) ? _primary.LocationId : _fallback.LocationId;
        public Transform CustomerSpawnRoot => Pick(_primary?.CustomerSpawnRoot, _fallback.CustomerSpawnRoot);
        public Transform EntryLeft => Pick(_primary?.EntryLeft, _fallback.EntryLeft);
        public Transform EntryRight => Pick(_primary?.EntryRight, _fallback.EntryRight);
        public Transform ExitLeft => Pick(_primary?.ExitLeft, _fallback.ExitLeft);
        public Transform ExitRight => Pick(_primary?.ExitRight, _fallback.ExitRight);
        public Transform ShopApproach => Pick(_primary?.ShopApproach, _fallback.ShopApproach);
        public IReadOnlyList<Transform> LaneAnchors => Pick(_primary?.LaneAnchors, _fallback.LaneAnchors);
        public IReadOnlyList<Transform> BubbleSlots => Pick(_primary?.BubbleSlots, _fallback.BubbleSlots);

        private static Transform Pick(Transform primary, Transform fallback) => primary != null ? primary : fallback;

        private static IReadOnlyList<Transform> Pick(IReadOnlyList<Transform> primary, IReadOnlyList<Transform> fallback)
            => primary is { Count: > 0 } ? primary : fallback ?? Array.Empty<Transform>();
    }
}
