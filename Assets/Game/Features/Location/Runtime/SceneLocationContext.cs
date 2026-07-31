using System;
using System.Collections.Generic;
using Game.Location.API;
using UnityEngine;

namespace Game.Location.Runtime
{
    public sealed class SceneLocationContext : ILocationContext
    {
        private readonly Transform[] _laneAnchors;

        public SceneLocationContext(
            Transform customerSpawnRoot,
            Transform entryLeft,
            Transform entryRight,
            Transform shopApproach,
            Transform[] laneAnchors,
            Transform exitLeft,
            Transform exitRight)
        {
            CustomerSpawnRoot = customerSpawnRoot;
            EntryLeft = entryLeft;
            EntryRight = entryRight;
            ShopApproach = shopApproach;
            _laneAnchors = laneAnchors ?? Array.Empty<Transform>();
            ExitLeft = exitLeft;
            ExitRight = exitRight;
        }

        public bool IsBound => false;
        public string LocationId => null;
        public Transform CustomerSpawnRoot { get; }
        public Transform EntryLeft { get; }
        public Transform EntryRight { get; }
        public Transform ExitLeft { get; }
        public Transform ExitRight { get; }
        public Transform ShopApproach { get; }
        public IReadOnlyList<Transform> LaneAnchors => _laneAnchors;
    }
}
