using System;
using System.Collections.Generic;
using Game.Location.API;
using UnityEngine;

namespace Game.Location.Runtime
{
    [DisallowMultipleComponent]
    public sealed class LocationController : MonoBehaviour, ILocationContext
    {
        [SerializeField] private string _locationId;
        [SerializeField] private Transform _customerSpawnRoot;
        [SerializeField] private Transform _entryLeft;
        [SerializeField] private Transform _entryRight;
        [SerializeField] private Transform _exitLeft;
        [SerializeField] private Transform _exitRight;
        [SerializeField] private Transform _shopApproach;
        [SerializeField] private Transform[] _laneAnchors;

        public bool IsBound => true;
        public string LocationId => _locationId;
        public Transform CustomerSpawnRoot => _customerSpawnRoot;
        public Transform EntryLeft => _entryLeft;
        public Transform EntryRight => _entryRight;
        public Transform ExitLeft => _exitLeft;
        public Transform ExitRight => _exitRight;
        public Transform ShopApproach => _shopApproach;
        public IReadOnlyList<Transform> LaneAnchors => _laneAnchors ?? Array.Empty<Transform>();
    }
}
