using UnityEngine;

namespace Infrastructure.ResourceAnimations
{
    [CreateAssetMenu(fileName = "ResourceAnimationSettings", menuName = "Game/UI/Resource Animation Settings")]
    public sealed class ResourceAnimationSettings : ScriptableObject
    {
        [SerializeField] private ResourceParticleView _particlePrefab;
        [SerializeField, Min(1)] private int _maxParticles = 6;
        [SerializeField, Min(0f)] private float _duration = 0.65f;
        [SerializeField, Min(0f)] private float _stagger = 0.04f;
        [SerializeField, Min(0f)] private float _bezierSpread = 120f;
        [SerializeField, Min(0f)] private float _startScale = 0.8f;
        [SerializeField, Min(0f)] private float _endScale = 1f;
        [SerializeField] private AnimationCurve _positionEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve _scaleEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private Sprite _fallbackSprite;
        [SerializeField] private int _sortingOrder = 3500;

        public ResourceParticleView ParticlePrefab => _particlePrefab;
        public int MaxParticles => Mathf.Max(1, _maxParticles);
        public float Duration => Mathf.Max(0f, _duration);
        public float Stagger => Mathf.Max(0f, _stagger);
        public float BezierSpread => Mathf.Max(0f, _bezierSpread);
        public float StartScale => Mathf.Max(0f, _startScale);
        public float EndScale => Mathf.Max(0f, _endScale);
        public AnimationCurve PositionEase => _positionEase ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public AnimationCurve ScaleEase => _scaleEase ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public Sprite FallbackSprite => _fallbackSprite;
        public int SortingOrder => _sortingOrder;

        public static ResourceAnimationSettings CreateDefault()
        {
            var settings = CreateInstance<ResourceAnimationSettings>();
            settings.name = nameof(ResourceAnimationSettings);
            return settings;
        }
    }
}
