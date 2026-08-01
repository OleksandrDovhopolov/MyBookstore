using System;
using System.Collections.Generic;
using Game.Location.API;
using UnityEngine;

namespace Book.Sell.UI.Customer
{
    public interface IBubbleSlotAllocator
    {
        int Capacity { get; }
        Transform Acquire(string customerId);
        void Release(string customerId);
    }

    public sealed class BubbleSlotAllocator : IBubbleSlotAllocator
    {
        private readonly ILocationContext _location;
        private readonly Dictionary<string, int> _ownerByCustomer = new();

        private Transform[] _slots;
        private string[] _owners;
        private bool _initialized;
        private bool _overflowWarned;

        public BubbleSlotAllocator(ILocationContext location)
        {
            _location = location;
        }

        public int Capacity
        {
            get
            {
                EnsureInitialized();
                return _slots.Length;
            }
        }

        public Transform Acquire(string customerId)
        {
            if (string.IsNullOrEmpty(customerId)) return null;
            EnsureInitialized();
            if (_slots.Length == 0) return null;

            if (_ownerByCustomer.TryGetValue(customerId, out var ownedIndex))
            {
                if (IsValidSlotIndex(ownedIndex) && _slots[ownedIndex] != null)
                    return _slots[ownedIndex];

                Release(customerId);
            }

            for (var i = 0; i < _slots.Length; i++)
            {
                if (_owners[i] != null || _slots[i] == null) continue;

                _owners[i] = customerId;
                _ownerByCustomer[customerId] = i;
                return _slots[i];
            }

            WarnOverflowOnce();
            return null;
        }

        public void Release(string customerId)
        {
            if (string.IsNullOrEmpty(customerId)) return;
            EnsureInitialized();

            if (!_ownerByCustomer.Remove(customerId, out var index)) return;
            if (IsValidSlotIndex(index) && _owners[index] == customerId)
                _owners[index] = null;
        }

        private void EnsureInitialized()
        {
            var source = _location?.BubbleSlots;
            if (_initialized && (_slots.Length > 0 || source is not { Count: > 0 })) return;

            if (source is not { Count: > 0 })
            {
                _slots = Array.Empty<Transform>();
                _owners = Array.Empty<string>();
                _initialized = true;
                return;
            }

            _slots = new Transform[source.Count];
            for (var i = 0; i < source.Count; i++)
                _slots[i] = source[i];
            _owners = new string[_slots.Length];
            _initialized = true;
        }

        private bool IsValidSlotIndex(int index) => index >= 0 && index < _slots.Length;

        private void WarnOverflowOnce()
        {
            if (_overflowWarned) return;
            _overflowWarned = true;
            Debug.LogWarning("[BubbleSlotAllocator] All authored customer bubble slots are occupied. Falling back to customer head anchors.");
        }
    }
}
