using Infrastructure.TutorialUI;
using UnityEngine;

namespace Game.Tutorial.Presentation
{
    /// <summary>
    /// Tunables + prefab refs for the tutorial overlay. Assigned on BootstrapInstaller and passed to the DI
    /// binding (mirrors ResourceAnimationSettings). <see cref="CreateDefault"/> keeps DI resilient when the
    /// asset is unassigned (overlay still works, just without a pointer sprite / custom text panel).
    /// </summary>
    [CreateAssetMenu(fileName = "TutorialOverlaySettings", menuName = "Game/Tutorial/Overlay Settings")]
    public sealed class TutorialOverlaySettings : ScriptableObject
    {
        [Header("Canvas")]
        [Tooltip("Overlay canvas sortingOrder. 3600 = above System (3000) & resource anims (3500), below Develop (4000).")]
        [SerializeField] private int _sortingOrder = 3600;

        [Header("Blackout")]
        [SerializeField] private Color _blackoutColor = new(0f, 0f, 0f, 0.7f);

        [Header("Pointer")]
        [Tooltip("Optional pointer prefab (its root has a TutorialPointerView). When assigned, it owns the " +
                 "arrow's visuals and the sprite/bounce fields below are ignored. When null, a pointer is " +
                 "built at runtime from those fields.")]
        [SerializeField] private TutorialPointerView _pointerPrefab;
        [SerializeField] private Sprite _pointerSprite;
        [SerializeField, Min(0f)] private float _pointerBounceAmplitude = 10f;
        [SerializeField, Min(0f)] private float _pointerBounceSpeed = 4f;

        [Header("Highlight")]
        [Tooltip("Extra padding (UI units) added around a highlighted target's rect when cutting the hole.")]
        [SerializeField, Min(0f)] private float _holePadding = 16f;

        [Header("Text panel")]
        [Tooltip("Prefab with a TutorialTextPanelView on its root.")]
        [SerializeField] private TutorialTextPanelView _textPanelPrefab;

        public int SortingOrder => _sortingOrder;
        public Color BlackoutColor => _blackoutColor;
        public TutorialPointerView PointerPrefab => _pointerPrefab;
        public Sprite PointerSprite => _pointerSprite;
        public float PointerBounceAmplitude => _pointerBounceAmplitude;
        public float PointerBounceSpeed => _pointerBounceSpeed;
        public float HolePadding => _holePadding;
        public TutorialTextPanelView TextPanelPrefab => _textPanelPrefab;

        public static TutorialOverlaySettings CreateDefault() => CreateInstance<TutorialOverlaySettings>();
    }
}
