using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Decor.UI
{
    /// <summary>
    /// A UI anchor for a single decor slot inside <see cref="DecorPlacementWindow"/>. One instance per
    /// slot, authored in the window prefab; its position/scale live in the prefab and the link to the
    /// config is the string <see cref="SlotId"/> (must match an id in bookshops.json).
    /// The view is "dumb": it renders empty/placed/highlighted states and plays place/remove tweens,
    /// while all placement logic and service calls stay in the controller.
    /// </summary>
    public sealed class DecorSlotAnchorView : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Must match a slot id from bookshops.json, e.g. cart_table_1.")]
        [SerializeField] private string _slotId;

        [Header("Empty state")]
        [SerializeField] private Button _markerButton;   // pin/marker shown when the slot is empty
        [SerializeField] private GameObject _highlight;   // shown when this empty slot is a valid target

        [Header("Placed state")]
        [SerializeField] private Image _placedDecorImage; // decor visual when the slot is occupied
        [SerializeField] private Button _placedButton;    // click on placed decor → opens the slot HUD
        [SerializeField] private CanvasGroup _placedGroup; // drives the place/remove alpha tween
        [SerializeField] private GameObject _selectedOutline; // optional outline when this slot is selected

        [Header("Animation")]
        [SerializeField, Min(0f)] private float _placeDuration = 0.25f;
        [SerializeField, Min(0f)] private float _removeDuration = 0.2f;

        private Sequence _activeTween;

        public string SlotId => _slotId;

        /// <summary>Raised when the empty-slot marker is clicked (decor-first placement target).</summary>
        public event Action OnMarkerClicked;

        /// <summary>Raised when the placed decor is clicked (opens Replace/Remove HUD).</summary>
        public event Action OnPlacedClicked;

        private void Awake()
        {
            if (_markerButton != null) _markerButton.onClick.AddListener(RaiseMarkerClicked);
            if (_placedButton != null) _placedButton.onClick.AddListener(RaisePlacedClicked);
        }

        /// <summary>Empty slot: show the marker, hide the decor. Marker is non-interactable until highlighted.</summary>
        public void SetEmpty()
        {
            KillActiveTween();

            if (_placedDecorImage != null)
            {
                _placedDecorImage.sprite = null;
                _placedDecorImage.gameObject.SetActive(false);
            }
            if (_placedButton != null) _placedButton.interactable = false;

            if (_markerButton != null) _markerButton.gameObject.SetActive(true);
            SetHighlighted(false);
            if (_selectedOutline != null) _selectedOutline.SetActive(false);
        }

        /// <summary>Occupied slot: show the decor sprite (fully visible), hide the marker.</summary>
        public void SetPlaced(Sprite sprite)
        {
            KillActiveTween();

            SetHighlighted(false);
            if (_markerButton != null) _markerButton.gameObject.SetActive(false);

            if (_placedDecorImage != null)
            {
                _placedDecorImage.sprite = sprite;
                _placedDecorImage.gameObject.SetActive(true);
                _placedDecorImage.transform.localScale = Vector3.one;
            }
            if (_placedGroup != null) _placedGroup.alpha = 1f;
            if (_placedButton != null) _placedButton.interactable = true;
        }

        /// <summary>Toggle the compatible-target highlight and marker interactivity (empty slots only).</summary>
        public void SetHighlighted(bool highlighted)
        {
            if (_highlight != null) _highlight.SetActive(highlighted);
            if (_markerButton != null) _markerButton.interactable = highlighted;
        }

        public void SetSelectedOutline(bool selected)
        {
            if (_selectedOutline != null) _selectedOutline.SetActive(selected);
        }

        /// <summary>Place animation: scale 0.85 → 1.08 → 1.0, alpha 0 → 1. Visual only.</summary>
        public void PlayPlaceTween()
        {
            KillActiveTween();
            if (_placedDecorImage == null) return;

            var target = _placedDecorImage.transform;
            SetScale(target, 0.85f);
            if (_placedGroup != null) _placedGroup.alpha = 0f;

            var overshoot = _placeDuration * 0.6f;
            var settle = _placeDuration - overshoot;

            _activeTween = DOTween.Sequence()
                .SetTarget(this)
                .SetUpdate(true)
                .Append(ScaleTween(target, 1.08f, overshoot).SetEase(Ease.OutQuad))
                .Append(ScaleTween(target, 1.0f, settle).SetEase(Ease.OutQuad));

            if (_placedGroup != null)
                _activeTween.Insert(0f, AlphaTween(1f, _placeDuration).SetEase(Ease.OutQuad));
        }

        /// <summary>Remove animation: scale 1.0 → 0.85, alpha 1 → 0, then <paramref name="onComplete"/>.</summary>
        public void PlayRemoveTween(Action onComplete)
        {
            KillActiveTween();
            if (_placedDecorImage == null)
            {
                onComplete?.Invoke();
                return;
            }

            var target = _placedDecorImage.transform;

            _activeTween = DOTween.Sequence()
                .SetTarget(this)
                .SetUpdate(true)
                .Join(ScaleTween(target, 0.85f, _removeDuration).SetEase(Ease.InQuad));

            if (_placedGroup != null)
                _activeTween.Join(AlphaTween(0f, _removeDuration).SetEase(Ease.InQuad));

            _activeTween.OnComplete(() => onComplete?.Invoke());
        }

        private Tween ScaleTween(Transform target, float value, float duration) =>
            DOTween.To(() => target.localScale.x, x => SetScale(target, x), value, duration);

        private Tween AlphaTween(float value, float duration) =>
            DOTween.To(() => _placedGroup.alpha, a => _placedGroup.alpha = a, value, duration);

        private static void SetScale(Transform target, float value) =>
            target.localScale = new Vector3(value, value, value);

        private void RaiseMarkerClicked() => OnMarkerClicked?.Invoke();

        private void RaisePlacedClicked() => OnPlacedClicked?.Invoke();

        private void KillActiveTween()
        {
            if (_activeTween == null || !_activeTween.IsActive()) return;
            _activeTween.Kill(false);
            _activeTween = null;
        }

        private void OnDestroy()
        {
            KillActiveTween();
            if (_markerButton != null) _markerButton.onClick.RemoveListener(RaiseMarkerClicked);
            if (_placedButton != null) _placedButton.onClick.RemoveListener(RaisePlacedClicked);
        }
    }
}
