using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Bootstrap.Loading
{
    // Минимальный лоадер для бутстрапа. Юзер собирает префаб с этим компонентом;
    // на префаб биндятся _rootGroup / _progressBar / _statusText.
    // Для retry — повесь UnityEvent на кнопку, вызывающий NotifyRetryClicked().
    public sealed class LoadingScreenView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _rootGroup;

        [Tooltip("Image with Type = Filled; fillAmount is driven by smoothed normalized progress.")]
        [SerializeField] private Image _progressBar;

        [Tooltip("How fast the bar catches up with the reported progress. 0 = no smoothing (instant).")]
        [SerializeField, Min(0f)] private float _progressCatchUpSpeed = 8f;

        [Tooltip("Floor speed in fillAmount units per second, so the bar always arrives instead of " +
                 "asymptotically crawling the last percent.")]
        [SerializeField, Min(0f)] private float _progressMinSpeed = 0.15f;

        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private GameObject _errorRoot;
        [SerializeField] private TextMeshProUGUI _errorText;

        private const float ProgressSnapEpsilon = 0.0005f;

        private UniTaskCompletionSource<bool> _retryTcs;
        private float _targetProgress;
        private float _displayedProgress;
        // View живёт в boot-сцене и умирает при SceneManager.LoadSceneAsync(Single).
        // Это ожидаемое поведение — entry point после перехода в view уже не лезет.

        private void OnEnable()
        {
            _targetProgress = 0f;
            _displayedProgress = 0f;

            if (_progressBar != null)
                _progressBar.fillAmount = 0f;
        }

        private void Update()
        {
            if (_progressBar == null) return;

            if (_progressCatchUpSpeed <= 0f || _targetProgress < _displayedProgress)
            {
                if (_displayedProgress != _targetProgress)
                    SetDisplayedProgress(_targetProgress);
                return;
            }

            var remaining = _targetProgress - _displayedProgress;
            if (remaining <= ProgressSnapEpsilon)
            {
                if (_displayedProgress != _targetProgress)
                    SetDisplayedProgress(_targetProgress);
                return;
            }

            var dt = Time.unscaledDeltaTime;
            var exponentialT = 1f - Mathf.Exp(-_progressCatchUpSpeed * dt);
            var exponentialProgress = Mathf.Lerp(_displayedProgress, _targetProgress, exponentialT);
            var minimumProgress = Mathf.MoveTowards(
                _displayedProgress,
                _targetProgress,
                _progressMinSpeed * dt);
            var nextProgress = Mathf.Max(exponentialProgress, minimumProgress);

            if (_targetProgress - nextProgress <= ProgressSnapEpsilon)
                nextProgress = _targetProgress;

            SetDisplayedProgress(nextProgress);
        }

        public void SetVisible(bool isVisible)
        {
            if (_rootGroup != null)
            {
                _rootGroup.alpha = isVisible ? 1f : 0f;
                _rootGroup.interactable = isVisible;
                _rootGroup.blocksRaycasts = isVisible;
            }
            else
            {
                gameObject.SetActive(isVisible);
            }
        }

        public void SetProgress(float normalizedProgress) =>
            _targetProgress = Mathf.Clamp01(normalizedProgress);

        public void SetStatus(string status)
        {
            if (_statusText != null)
            {
                _statusText.text = status ?? string.Empty;
            }
        }

        public void SetError(string message)
        {
            if (_errorText != null)
            {
                _errorText.text = message ?? string.Empty;
            }
        }

        public void SetErrorVisible(bool isVisible)
        {
            if (_errorRoot != null)
            {
                _errorRoot.SetActive(isVisible);
            }
        }

        // Привязать к OnClick кнопки Retry в инспекторе.
        public void NotifyRetryClicked()
        {
            _retryTcs?.TrySetResult(true);
        }

        public async UniTask WaitForRetryClickAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _retryTcs = new UniTaskCompletionSource<bool>();
            await using var registration = ct.Register(() => _retryTcs.TrySetCanceled(ct));
            await _retryTcs.Task;
            _retryTcs = null;
        }

        private void SetDisplayedProgress(float normalizedProgress)
        {
            _displayedProgress = Mathf.Clamp01(normalizedProgress);
            _progressBar.fillAmount = _displayedProgress;
        }
    }
}
