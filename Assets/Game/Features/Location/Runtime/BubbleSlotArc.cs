using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Location.Runtime
{
    [DisallowMultipleComponent]
    public sealed class BubbleSlotArc : MonoBehaviour
    {
        [Tooltip("Slots in fill-priority order, not sibling order.")]
        [SerializeField] private Transform[] _slots;

        public IReadOnlyList<Transform> Slots => _slots ?? Array.Empty<Transform>();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_slots == null) return;

            var seen = new HashSet<Transform>();
            for (var i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot == null)
                {
                    Debug.LogWarning($"[BubbleSlotArc] Slot {i} is null.", this);
                    continue;
                }

                if (!seen.Add(slot))
                    Debug.LogWarning($"[BubbleSlotArc] Duplicate slot '{slot.name}'.", this);

                if (slot.parent != transform)
                    Debug.LogWarning($"[BubbleSlotArc] Slot '{slot.name}' must be a direct child of '{name}'.", this);
            }

            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child != null && !seen.Contains(child))
                    Debug.LogWarning($"[BubbleSlotArc] Direct child '{child.name}' is not present in the fill-order slots.", this);
            }
        }
#endif
    }
}
