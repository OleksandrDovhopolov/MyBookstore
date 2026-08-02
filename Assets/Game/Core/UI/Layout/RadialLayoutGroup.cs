using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Layout
{
    /// <summary>
    /// Lays direct children out on one or more concentric arcs. Authoring tool: it positions the
    /// children in the editor and the result is saved into the prefab. It does not run at runtime;
    /// call <see cref="Rebuild"/> explicitly if a runtime relayout is ever needed.
    ///
    /// Writes <c>localPosition</c>. That is exact for plain Transforms (the bubble-arc slots). On a
    /// RectTransform it maps to anchoredPosition3D only while the min/max anchors coincide, so this
    /// is not a drop-in replacement for a uGUI LayoutGroup.
    /// </summary>
    [AddComponentMenu("Layout/Radial Layout Group")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class RadialLayoutGroup : MonoBehaviour
    {
        [Header("Arc")]
        [SerializeField] private float _radius = 5f;
        [SerializeField] private float _spaceAngle = 10f;
        [Tooltip("Elements per row. Rows are filled in sibling order.")]
        [Min(1)]
        [SerializeField] private int _maxCountInRow = 5;
        [Tooltip("Row spacing along the radius. Negative pushes later rows outward.")]
        [SerializeField] private float _offsetBetweenRows;
        [SerializeField] private float _startAngle;

        [Header("Spacing")]
        [Tooltip("Scales generated local X/Y positions around this pivot. Use X > 1 to spread slots horizontally without changing row count.")]
        [SerializeField] private Vector2 _positionScale = Vector2.one;
        [Tooltip("Extra local offset applied per row. Positive Y raises later rows; X can stagger later rows manually.")]
        [SerializeField] private Vector3 _rowOffset;

        [Header("Options")]
        [SerializeField] private bool _needAttachPivotToFirstElement;
        [SerializeField] private bool _updateItemsRotation;
        [SerializeField] private bool _inverseOrder;
        [SerializeField] private Vector3 _itemsOffset;

#if UNITY_EDITOR
        [Header("Gizmos")]
        [Tooltip("World-space radius of the per-slot gizmo sphere.")]
        [SerializeField] private float _gizmoSlotRadius = 0.15f;
#endif

        private readonly List<Transform> _elements = new();

        public float Radius { get => _radius; set => _radius = value; }
        public float SpaceAngle { get => _spaceAngle; set => _spaceAngle = value; }
        public int MaxCountInRow { get => RowCapacity; set => _maxCountInRow = Mathf.Max(1, value); }
        public float OffsetBetweenRows { get => _offsetBetweenRows; set => _offsetBetweenRows = value; }
        public float StartAngle { get => _startAngle; set => _startAngle = value; }
        public Vector2 PositionScale { get => _positionScale; set => _positionScale = value; }
        public Vector3 RowOffset { get => _rowOffset; set => _rowOffset = value; }
        public bool NeedAttachPivotToFirstElement { get => _needAttachPivotToFirstElement; set => _needAttachPivotToFirstElement = value; }
        public bool UpdateItemsRotation { get => _updateItemsRotation; set => _updateItemsRotation = value; }
        public bool InverseOrder { get => _inverseOrder; set => _inverseOrder = value; }
        public Vector3 ItemsOffset { get => _itemsOffset; set => _itemsOffset = value; }

        private int RowCapacity => Mathf.Max(1, _maxCountInRow);

        public void Rebuild() => UpdateRadialLayoutGroup();

        private void UpdateRadialLayoutGroup()
        {
            CollectActiveElements();

            var rowCapacity = RowCapacity;
            var count = _elements.Count;
            var rowsCount = count / rowCapacity;
            var direction = Quaternion.AngleAxis(_startAngle, -Vector3.forward) * new Vector3(0f, _radius, 0f);

            for (var i = 0; i < count; i++)
            {
                var position = ResolveLocalPosition(i, count, rowsCount, rowCapacity, direction, out var angle);
                var currentElement = _inverseOrder ? _elements[^(i + 1)] : _elements[i];
                if (currentElement == null) continue;

                currentElement.localPosition = position + _itemsOffset;
                if (_updateItemsRotation)
                    currentElement.localRotation = Quaternion.Euler(new Vector3(0f, 0f, -angle));
            }
        }

        private Vector3 ResolveLocalPosition(
            int i, int count, int rowsCount, int rowCapacity, Vector3 direction, out float angle)
        {
            var currentRow = i / rowCapacity;
            var indexInRow = i % rowCapacity;
            var elementsInRow = currentRow == rowsCount ? count % rowCapacity : rowCapacity;
            var rowAngleOffset = (elementsInRow - 1) / 2f * _spaceAngle;
            angle = -rowAngleOffset + _spaceAngle * indexInRow;

            var position = Quaternion.AngleAxis(angle, -Vector3.forward) * direction;
            position -= (_needAttachPivotToFirstElement ? direction : Vector3.zero)
                        + (_offsetBetweenRows * currentRow) * direction.normalized;
            position = new Vector3(position.x * _positionScale.x, position.y * _positionScale.y, position.z);
            position += _rowOffset * currentRow;
            return position;
        }

        private void CollectActiveElements()
        {
            _elements.Clear();
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child == null || !child.gameObject.activeSelf) continue;
                _elements.Add(child);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _maxCountInRow = Mathf.Max(1, _maxCountInRow);
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += RebuildIfAlive;
        }

        private void OnTransformChildrenChanged()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += RebuildIfAlive;
        }

        private void RebuildIfAlive()
        {
            UnityEditor.EditorApplication.delayCall -= RebuildIfAlive;
            if (this == null) return;
            UpdateRadialLayoutGroup();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.9f);
            CollectActiveElements();

            var rowCapacity = RowCapacity;
            var count = _elements.Count;
            var rowsCount = count / rowCapacity;
            var direction = Quaternion.AngleAxis(_startAngle, -Vector3.forward) * new Vector3(0f, _radius, 0f);
            var origin = transform.position - direction + _itemsOffset;

            Gizmos.DrawWireSphere(origin, _radius);

            for (var i = 0; i < count; i++)
            {
                var position = ResolveLocalPosition(i, count, rowsCount, rowCapacity, direction, out _);
                Gizmos.DrawWireSphere(transform.position + position + _itemsOffset, _gizmoSlotRadius);

                if (i % rowCapacity == 0) continue;
                var previous = ResolveLocalPosition(i - 1, count, rowsCount, rowCapacity, direction, out _);
                Gizmos.DrawLine(
                    transform.position + previous + _itemsOffset,
                    transform.position + position + _itemsOffset);
            }

            for (var i = -1; i < 2; i++)
            {
                var sectorAngle = 360f - _startAngle + i * _spaceAngle;
                var sectorDirection = Quaternion.AngleAxis(sectorAngle, Vector3.forward) * new Vector3(0f, _radius, 0f);
                Gizmos.DrawLine(origin, transform.position + sectorDirection - direction + _itemsOffset);
            }
        }
#endif
    }
}
