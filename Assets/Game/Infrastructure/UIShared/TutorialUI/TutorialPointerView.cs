using UnityEngine;
using UnityEngine.UI;

namespace Infrastructure.TutorialUI
{
    /// <summary>
    /// Animated pointer (hand/arrow) that hovers over a target with a vertical sine bounce. Non-interactive
    /// (raycastTarget off) so it never eats the click meant for the highlighted button. Positions itself in
    /// its parent (overlay) local space via <see cref="ScreenRectUtility"/> every frame.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TutorialPointerView : MonoBehaviour
    {
        [SerializeField] private float _bounceAmplitude = 10f;
        [SerializeField] private float _bounceSpeed = 4f;

        private RectTransform _rt;
        private Image _image;
        private RectTransform _target;

        private RectTransform RectTransform => _rt != null ? _rt : _rt = (RectTransform)transform;
        private RectTransform ParentRect => transform.parent as RectTransform;

        private void Awake()
        {
            _image = GetComponent<Image>();
            if (_image == null) _image = gameObject.AddComponent<Image>();
            _image.raycastTarget = false;
        }

        public void Configure(Sprite sprite, float amplitude, float speed)
        {
            if (_image == null) _image = GetComponent<Image>();
            if (sprite != null) _image.sprite = sprite;
            _image.enabled = sprite != null;
            if (amplitude > 0f) _bounceAmplitude = amplitude;
            if (speed > 0f) _bounceSpeed = speed;
        }

        public void PointAt(RectTransform target)
        {
            _target = target;
            gameObject.SetActive(true);
        }

        public void HideView()
        {
            _target = null;
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_target == null) return;
            var parent = ParentRect;
            if (parent == null) return;
            if (!ScreenRectUtility.TryGetLocalRect(_target, parent, out var rect)) return;

            var bounce = Mathf.Sin(Time.unscaledTime * _bounceSpeed) * _bounceAmplitude;
            RectTransform.anchoredPosition = new Vector2(rect.center.x, rect.yMax + bounce);
        }
    }
}
