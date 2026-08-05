using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Infrastructure.ResourceAnimations;
using TMPro;
using UnityEngine;

namespace UIShared
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class ResourceCounterTargetTag : MonoBehaviour, IResourceCounterTarget
    {
        [Header("Resource")]
        [SerializeField] private string _resourceId = "Gold";
        [SerializeField] private TMP_Text _amountLabel;

        [Header("Count Up")]
        [Tooltip("Ramp after a coin flight (day payout).")]
        [SerializeField] private float _countUpDuration = 1f;

        [Tooltip("Ramp for ordinary balance changes — purchases, rewards. Kept short so spending " +
                 "still feels immediate.")]
        [SerializeField] private float _changeDuration = 0.3f;

        [Header("Feedback")]
        [SerializeField] private Transform _pulseRoot;
        [SerializeField] private float _pulseScale = 1.08f;
        [SerializeField] private float _pulseDuration = 0.18f;

        private RectTransform _rt;
        private CancellationTokenSource _countUpCts;
        private Tween _feedbackTween;
        private Vector3 _pulseStartScale = Vector3.one;

        public string ResourceId => _resourceId;
        //public RectTransform RectTransform => _rt != null ? _rt : _rt = (RectTransform)transform;
        public RectTransform RectTransform => (RectTransform)_pulseRoot;
        public int DisplayedAmount { get; private set; }

        private Transform PulseRoot => _pulseRoot != null ? _pulseRoot : transform;

        private void Awake()
        {
            _pulseStartScale = PulseRoot.localScale;
        }

        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(_resourceId)) return;

            ResourceCounterTargets.Register(this);
            ResourceAnimationTargets.Register(ResourceAnimationTargetIds.Resource(_resourceId), RectTransform);
        }

        private void OnDisable()
        {
            CancelCountUp();
            _feedbackTween?.Kill();
            _feedbackTween = null;

            if (string.IsNullOrWhiteSpace(_resourceId)) return;

            ResourceCounterTargets.Unregister(this);
            ResourceAnimationTargets.Unregister(ResourceAnimationTargetIds.Resource(_resourceId), RectTransform);
        }

        private void OnDestroy()
        {
            CancelCountUp();
            _feedbackTween?.Kill();
            _feedbackTween = null;
        }

        public void SetAmountImmediate(int amount)
        {
            CancelCountUp();
            SetDisplayedAmount(amount);
        }

        public UniTask AnimateAmountToAsync(int amount, CancellationToken ct = default)
            => AnimateAsync(amount, _countUpDuration, ct);

        public UniTask AnimateChangeAsync(int amount, CancellationToken ct = default)
            => AnimateAsync(amount, _changeDuration, ct);

        private async UniTask AnimateAsync(int amount, float durationSeconds, CancellationToken ct)
        {
            CancelCountUp();

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, this.GetCancellationTokenOnDestroy());
            _countUpCts = linkedCts;
            var token = linkedCts.Token;

            var from = DisplayedAmount;
            var to = Mathf.Max(0, amount);
            var duration = Mathf.Max(0f, durationSeconds);

            try
            {
                if (duration <= 0f || from == to)
                {
                    SetDisplayedAmount(to);
                    return;
                }

                var elapsed = 0f;
                while (elapsed < duration)
                {
                    token.ThrowIfCancellationRequested();
                    elapsed += Time.unscaledDeltaTime;
                    var t = Mathf.Clamp01(elapsed / duration);
                    SetDisplayedAmount(Mathf.RoundToInt(Mathf.Lerp(from, to, t)));
                    await UniTask.NextFrame(token);
                }

                SetDisplayedAmount(to);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (ReferenceEquals(_countUpCts, linkedCts))
                {
                    _countUpCts.Dispose();
                    _countUpCts = null;
                }
            }
        }

        public void PlayArriveFeedback()
        {
            var pulseRoot = PulseRoot;
            _feedbackTween?.Kill();
            pulseRoot.localScale = _pulseStartScale;

            _feedbackTween = DOTween.Sequence()
                .SetUpdate(true)
                .Append(pulseRoot.DOScale(_pulseStartScale * Mathf.Max(1f, _pulseScale), Mathf.Max(0.01f, _pulseDuration)))
                .Append(pulseRoot.DOScale(_pulseStartScale, Mathf.Max(0.01f, _pulseDuration)))
                .OnKill(() => _feedbackTween = null);
        }

        private void SetDisplayedAmount(int amount)
        {
            DisplayedAmount = Mathf.Max(0, amount);
            if (_amountLabel != null)
                _amountLabel.text = DisplayedAmount.ToString();
        }

        private void CancelCountUp()
        {
            if (_countUpCts == null) return;

            _countUpCts.Cancel();
            _countUpCts.Dispose();
            _countUpCts = null;
        }
    }
}
