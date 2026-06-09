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
        [SerializeField] private Slider _progressBar;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private GameObject _errorRoot;
        [SerializeField] private TextMeshProUGUI _errorText;

        private UniTaskCompletionSource<bool> _retryTcs;

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

        public void SetProgress(float normalizedProgress)
        {
            if (_progressBar != null)
            {
                _progressBar.value = normalizedProgress;
            }
        }

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
    }
}
