using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Infrastructure.ResourceAnimations;
using SpriteService;
using UnityEngine;

namespace Game.UI.ResourceAnimations
{
    public sealed class ResourceAnimationService : IResourceAnimationService
    {
        private const string LogPrefix = "[ResourceAnimation]";

        private readonly ResourceAnimationSettings _settings;
        private readonly IUICanvasRoot _canvasRoot;
        private readonly IUiSpriteProvider _sprites;
        private readonly IResourceAnimationTargetRegistry _targets;

        private RectTransform _animationRoot;
        private ResourceParticlePool _pool;
        private bool _warnedMissingPrefab;

        public ResourceAnimationService(
            ResourceAnimationSettings settings,
            IUICanvasRoot canvasRoot,
            IUiSpriteProvider sprites,
            IResourceAnimationTargetRegistry targets)
        {
            _settings = settings != null ? settings : ResourceAnimationSettings.CreateDefault();
            _canvasRoot = canvasRoot;
            _sprites = sprites;
            _targets = targets;
        }

        public async UniTask PlayAsync(ResourceAnimationRequest request, CancellationToken ct = default)
        {
            var particleCount = ResourceAnimationRequestRules.ResolveParticleCount(request.Amount, _settings.MaxParticles);
            if (particleCount == 0) return;

            var root = EnsureRoot();
            if (root == null) return;

            if (!ResourceAnimationEndpointResolver.TryResolve(request.From, root, _targets, out var from) ||
                !ResourceAnimationEndpointResolver.TryResolve(request.To, root, _targets, out var to))
            {
                Debug.LogWarning($"{LogPrefix} Cannot resolve animation endpoints.");
                return;
            }

            var pool = EnsurePool(root);
            if (pool == null || !pool.CanSpawn)
            {
                WarnMissingPrefabOnce();
                return;
            }

            var sprite = await ResolveSpriteAsync(request, ct);
            var particles = new List<ResourceParticleView>(particleCount);
            Sequence sequence = null;

            try
            {
                sequence = DOTween.Sequence()
                    .SetUpdate(true)
                    .SetTarget(this);

                for (var i = 0; i < particleCount; i++)
                {
                    var particle = pool.Get();
                    particles.Add(particle);

                    var label = i == 0 && Mathf.Abs(request.Amount) > 1 ? FormatAmount(request.Amount) : string.Empty;
                    particle.Bind(sprite, label);
                    particle.RectTransform.anchoredPosition = from;
                    particle.RectTransform.localScale = Vector3.one * _settings.StartScale;

                    // Hidden until its staggered turn to fly, so later particles don't sit visible
                    // at the source point while they wait. Revealed by the callback inserted below.
                    particle.CanvasGroup.alpha = 0f;

                    var delay = i * _settings.Stagger;
                    var control = BuildControlPoint(from, to);
                    sequence.InsertCallback(delay, () => particle.CanvasGroup.alpha = 1f);
                    var move = DOTween.To(
                            () => 0f,
                            t => particle.RectTransform.anchoredPosition = EvaluateQuadraticBezier(from, control, to, t),
                            1f,
                            _settings.Duration)
                        .SetEase(_settings.PositionEase)
                        .OnComplete(() => request.OnParticleArrived?.Invoke());

                    var scale = DOTween.To(
                            () => _settings.StartScale,
                            value => particle.RectTransform.localScale = Vector3.one * value,
                            _settings.EndScale,
                            _settings.Duration)
                        .SetEase(_settings.ScaleEase);

                    sequence.Insert(delay, move);
                    sequence.Insert(delay, scale);
                }

                await AwaitSequenceAsync(sequence, ct);
            }
            finally
            {
                if (sequence != null && sequence.IsActive())
                    sequence.Kill(false);

                for (var i = 0; i < particles.Count; i++)
                    pool.Release(particles[i]);
            }
        }

        private RectTransform EnsureRoot()
        {
            if (_animationRoot != null) return _animationRoot;

            var parent = _canvasRoot?.WindowsRoot != null ? _canvasRoot.WindowsRoot : _canvasRoot?.HudRoot;
            if (parent == null)
            {
                Debug.LogWarning($"{LogPrefix} UI canvas root is not available.");
                return null;
            }

            var go = new GameObject("ResourceAnimationRoot", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false);

            _animationRoot = (RectTransform)go.transform;
            _animationRoot.anchorMin = Vector2.zero;
            _animationRoot.anchorMax = Vector2.one;
            _animationRoot.offsetMin = Vector2.zero;
            _animationRoot.offsetMax = Vector2.zero;
            _animationRoot.localScale = Vector3.one;

            var canvas = go.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = _settings.SortingOrder;

            return _animationRoot;
        }

        private ResourceParticlePool EnsurePool(RectTransform root)
        {
            if (_pool != null) return _pool;
            _pool = new ResourceParticlePool(_settings.ParticlePrefab, root);
            return _pool;
        }

        private async UniTask<Sprite> ResolveSpriteAsync(ResourceAnimationRequest request, CancellationToken ct)
        {
            //TODO commented because useless now
            /*var spriteId = ResourceAnimationRequestRules.ResolveSpriteId(request);
            if (_sprites != null && !string.IsNullOrWhiteSpace(spriteId))
            {
                var sprite = await _sprites.GetSpriteAsync(spriteId, ct);
                if (sprite != null) return sprite;
            }*/

            return _settings.FallbackSprite;
        }

        private Vector2 BuildControlPoint(Vector2 from, Vector2 to)
        {
            var midpoint = (from + to) * 0.5f;
            var direction = to - from;
            var normal = direction.sqrMagnitude > 0.01f
                ? new Vector2(-direction.y, direction.x).normalized
                : Vector2.up;
            var signedSpread = UnityEngine.Random.Range(-_settings.BezierSpread, _settings.BezierSpread);
            return midpoint + normal * signedSpread;
        }

        private static Vector2 EvaluateQuadraticBezier(Vector2 from, Vector2 control, Vector2 to, float t)
        {
            var u = 1f - t;
            return u * u * from + 2f * u * t * control + t * t * to;
        }

        private static string FormatAmount(int amount) => amount > 0 ? $"+{amount}" : amount.ToString();

        private static async UniTask AwaitSequenceAsync(Sequence sequence, CancellationToken ct)
        {
            var completion = new UniTaskCompletionSource();

            using var registration = ct.Register(() =>
            {
                if (sequence != null && sequence.IsActive())
                    sequence.Kill(false);
                completion.TrySetCanceled(ct);
            });

            sequence.OnComplete(() => completion.TrySetResult());
            sequence.OnKill(() =>
            {
                if (!ct.IsCancellationRequested)
                    completion.TrySetResult();
            });

            sequence.Play();
            await completion.Task;
        }

        private void WarnMissingPrefabOnce()
        {
            if (_warnedMissingPrefab) return;
            _warnedMissingPrefab = true;
            Debug.LogWarning($"{LogPrefix} Particle prefab is not assigned.");
        }
    }
}
