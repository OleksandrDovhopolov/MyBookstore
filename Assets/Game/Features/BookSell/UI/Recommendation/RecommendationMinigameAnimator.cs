using System;
using DG.Tweening;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Book.Sell.UI
{
    public sealed class RecommendationMinigameAnimator : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private RectTransform _requestPanel;
        [SerializeField] private AnimatedShowHidePanel _shelfPanel;
        [SerializeField] private RectTransform _bookDetailRoot;
        [SerializeField] private RectTransform _resultRoot;
        [SerializeField] private GameObject _buttonsRoot;

        [Header("Result")]
        [SerializeField] private TMP_Text _resultText;
        [SerializeField] private Button _finishButton;

        [Header("Timings")]
        [SerializeField, Min(0f)] private float _requestDuration = 0.5625f;
        [SerializeField, Min(0f)] private float _requestSlideY = 24f;
        [SerializeField, Range(0.01f, 1f)] private float _requestScaleFrom = 0.98f;
        [SerializeField, Min(0f)] private float _detailShowDuration = 0.405f;
        [SerializeField, Min(0f)] private float _detailHideDuration = 0.27f;
        [SerializeField, Range(0.01f, 1f)] private float _detailScaleFrom = 0.97f;
        [SerializeField, Min(0f)] private float _resultRootDuration = 0.5625f;
        [SerializeField, Min(0f)] private float _resultTextDuration = 0.45f;
        [SerializeField, Min(0f)] private float _finishButtonDelay = 1.0125f;
        [SerializeField, Min(0f)] private float _finishButtonDuration = 0.405f;
        [SerializeField, Min(0f)] private float _buttonsFadeDuration = 0.405f;

        private Sequence _requestTween;
        private Sequence _detailTween;
        private Sequence _resultTween;
        private Tween _buttonsTween;
        private Vector2 _requestShownPosition;
        private bool _requestPositionCaptured;

        private void Awake() => CaptureRequestPosition();
        
        public void PrepareForRequest()
        {
            KillAll();
            HideButtons(instant: true);
            ShowBookDetailInstant();
            HideResultInstant();
            SetFinishButtonVisible(false, interactable: false);
        }

        public void PlayRequestIntro()
        {
            CaptureRequestPosition();
            PlayRequestPanelIntro();
            ShowButtons();
            if (_shelfPanel != null) _shelfPanel.Show();
        }

        public void HideSelectionInstant()
            => HideSelection(instant: true);

        public void HideSelection(bool instant = false, Action onComplete = null)
        {
            KillRequestTween();
            HideRequestPanelInstant();
            RunSelectionHide(instant, onComplete);

            if (instant) HideBookDetailInstant();
            else HideBookDetail();
        }

        public void ShowBookDetail()
        {
            if (_bookDetailRoot == null) return;
            KillDetailTween();
            _bookDetailRoot.gameObject.SetActive(true);

            var group = EnsureCanvasGroup(_bookDetailRoot.gameObject);
            group.alpha = 0f;
            group.interactable = true;
            group.blocksRaycasts = true;
            _bookDetailRoot.localScale = Vector3.one * _detailScaleFrom;

            _detailTween = DOTween.Sequence()
                .SetUpdate(true)
                .Join(DOTween.To(() => group.alpha, x => group.alpha = x, 1f, _detailShowDuration).SetEase(Ease.OutCubic))
                .Join(DOTween.To(() => _bookDetailRoot.localScale, x => _bookDetailRoot.localScale = x, Vector3.one, _detailShowDuration)
                    .SetEase(Ease.OutBack))
                .SetTarget(this);
        }

        /// <summary>
        /// Resets the detail area to fully visible without animating. The area hosts both the
        /// "nothing selected" placeholder and the selected-book content, so it stays on screen for the
        /// whole selection phase — only <see cref="HideSelection"/> (moving to the result) takes it away.
        /// </summary>
        public void ShowBookDetailInstant()
        {
            KillDetailTween();
            if (_bookDetailRoot == null) return;

            _bookDetailRoot.gameObject.SetActive(true);
            var group = EnsureCanvasGroup(_bookDetailRoot.gameObject);
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
            _bookDetailRoot.localScale = Vector3.one;
        }

        public void HideBookDetail(Action onComplete = null)
        {
            if (_bookDetailRoot == null)
            {
                onComplete?.Invoke();
                return;
            }

            KillDetailTween();
            var group = EnsureCanvasGroup(_bookDetailRoot.gameObject);
            group.interactable = false;
            group.blocksRaycasts = false;

            _detailTween = DOTween.Sequence()
                .SetUpdate(true)
                .Join(DOTween.To(() => group.alpha, x => group.alpha = x, 0f, _detailHideDuration).SetEase(Ease.InQuad))
                .OnComplete(() =>
                {
                    _bookDetailRoot.gameObject.SetActive(false);
                    _bookDetailRoot.localScale = Vector3.one;
                    onComplete?.Invoke();
                })
                .SetTarget(this);
        }

        public void HideBookDetailInstant()
        {
            KillDetailTween();
            if (_bookDetailRoot == null) return;

            var group = EnsureCanvasGroup(_bookDetailRoot.gameObject);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            _bookDetailRoot.localScale = Vector3.one;
            _bookDetailRoot.gameObject.SetActive(false);
        }

        public void PlayResult(string text)
        {
            if (_resultText != null)
                _resultText.text = text ?? string.Empty;

            KillResultTween();
            if (_resultRoot == null)
            {
                SetFinishButtonVisible(true, interactable: true);
                return;
            }

            _resultRoot.gameObject.SetActive(true);
            var rootGroup = EnsureCanvasGroup(_resultRoot.gameObject);
            rootGroup.alpha = 0f;
            rootGroup.interactable = true;
            rootGroup.blocksRaycasts = true;
            _resultRoot.localScale = Vector3.one * _detailScaleFrom;

            var textGroup = _resultText != null ? EnsureCanvasGroup(_resultText.gameObject) : null;
            if (textGroup != null) textGroup.alpha = 0f;
            SetFinishButtonVisible(false, interactable: false);

            _resultTween = DOTween.Sequence()
                .SetUpdate(true)
                .Append(DOTween.To(() => rootGroup.alpha, x => rootGroup.alpha = x, 1f, _resultRootDuration).SetEase(Ease.OutCubic))
                .Join(DOTween.To(() => _resultRoot.localScale, x => _resultRoot.localScale = x, Vector3.one, _resultRootDuration)
                    .SetEase(Ease.OutBack));

            if (textGroup != null)
                _resultTween.Append(DOTween.To(() => textGroup.alpha, x => textGroup.alpha = x, 1f, _resultTextDuration).SetEase(Ease.OutCubic));

            _resultTween.AppendInterval(_finishButtonDelay)
                .AppendCallback(() => SetFinishButtonVisible(true, interactable: false));

            var buttonGroup = _finishButton != null ? EnsureCanvasGroup(_finishButton.gameObject) : null;
            if (buttonGroup != null)
            {
                buttonGroup.alpha = 0f;
                _resultTween.Append(DOTween.To(() => buttonGroup.alpha, x => buttonGroup.alpha = x, 1f, _finishButtonDuration)
                    .SetEase(Ease.OutCubic));
            }

            _resultTween.OnComplete(() => SetFinishButtonVisible(true, interactable: true))
                .SetTarget(this);
        }

        public void HideResultInstant()
        {
            KillResultTween();
            SetFinishButtonVisible(false, interactable: false);
            if (_resultRoot == null) return;

            var rootGroup = EnsureCanvasGroup(_resultRoot.gameObject);
            rootGroup.alpha = 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
            _resultRoot.localScale = Vector3.one;
            _resultRoot.gameObject.SetActive(false);

            if (_resultText != null)
                EnsureCanvasGroup(_resultText.gameObject).alpha = 1f;
        }

        public void KillAll()
        {
            KillRequestTween();
            KillDetailTween();
            KillResultTween();
            KillButtonsTween();
        }

        private void PlayRequestPanelIntro()
        {
            if (_requestPanel == null) return;
            KillRequestTween();

            var group = EnsureCanvasGroup(_requestPanel.gameObject);
            _requestPanel.gameObject.SetActive(true);
            group.alpha = 0f;
            group.interactable = true;
            group.blocksRaycasts = true;
            _requestPanel.anchoredPosition = _requestShownPosition + new Vector2(0f, _requestSlideY);
            _requestPanel.localScale = Vector3.one * _requestScaleFrom;

            _requestTween = DOTween.Sequence()
                .SetUpdate(true)
                .Join(DOTween.To(() => group.alpha, x => group.alpha = x, 1f, _requestDuration).SetEase(Ease.OutCubic))
                .Join(DOTween.To(() => _requestPanel.anchoredPosition, x => _requestPanel.anchoredPosition = x, _requestShownPosition, _requestDuration)
                    .SetEase(Ease.OutCubic))
                .Join(DOTween.To(() => _requestPanel.localScale, x => _requestPanel.localScale = x, Vector3.one, _requestDuration)
                    .SetEase(Ease.OutCubic))
                .SetTarget(this);
        }

        private void HideRequestPanelInstant()
        {
            if (_requestPanel == null) return;

            var group = EnsureCanvasGroup(_requestPanel.gameObject);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            _requestPanel.anchoredPosition = _requestShownPosition;
            _requestPanel.localScale = Vector3.one;
        }

        private void ShowButtons()
        {
            if (_buttonsRoot == null) return;
            KillButtonsTween();

            _buttonsRoot.SetActive(true);
            var group = EnsureCanvasGroup(_buttonsRoot);
            group.alpha = 0f;
            group.interactable = true;
            group.blocksRaycasts = true;

            _buttonsTween = DOTween.To(() => group.alpha, x => group.alpha = x, 1f, _buttonsFadeDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetTarget(this);
        }

        private void HideButtons(bool instant, Action onComplete = null)
        {
            if (_buttonsRoot == null)
            {
                onComplete?.Invoke();
                return;
            }

            KillButtonsTween();
            var group = EnsureCanvasGroup(_buttonsRoot);
            group.interactable = false;
            group.blocksRaycasts = false;

            if (instant || _buttonsFadeDuration <= 0f)
            {
                group.alpha = 0f;
                onComplete?.Invoke();
                return;
            }

            _buttonsTween = DOTween.To(() => group.alpha, x => group.alpha = x, 0f, _buttonsFadeDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .SetTarget(this)
                .OnComplete(() => onComplete?.Invoke());
        }

        private void RunSelectionHide(bool instant, Action onComplete)
        {
            var pending = 0;
            if (_shelfPanel != null) pending++;
            if (_buttonsRoot != null) pending++;

            if (pending == 0)
            {
                onComplete?.Invoke();
                return;
            }

            void Complete()
            {
                pending--;
                if (pending == 0) onComplete?.Invoke();
            }

            if (_shelfPanel != null)
                _shelfPanel.Hide(instant, Complete);

            if (_buttonsRoot != null)
                HideButtons(instant, Complete);
        }

        private void CaptureRequestPosition()
        {
            if (_requestPanel == null || _requestPositionCaptured) return;
            _requestShownPosition = _requestPanel.anchoredPosition;
            _requestPositionCaptured = true;
        }

        private void SetFinishButtonVisible(bool visible, bool interactable)
        {
            if (_finishButton == null) return;
            _finishButton.gameObject.SetActive(visible);
            _finishButton.interactable = interactable;

            var group = EnsureCanvasGroup(_finishButton.gameObject);
            if (!visible) group.alpha = 0f;
            else if (interactable) group.alpha = 1f;
            group.interactable = visible && interactable;
            group.blocksRaycasts = visible && interactable;
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject target)
        {
            if (target == null) return null;
            var group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.AddComponent<CanvasGroup>();
        }

        private void KillRequestTween()
        {
            if (_requestTween != null && _requestTween.IsActive()) _requestTween.Kill(false);
            _requestTween = null;
        }

        private void KillDetailTween()
        {
            if (_detailTween != null && _detailTween.IsActive()) _detailTween.Kill(false);
            _detailTween = null;
        }

        private void KillResultTween()
        {
            if (_resultTween != null && _resultTween.IsActive()) _resultTween.Kill(false);
            _resultTween = null;
        }

        private void KillButtonsTween()
        {
            if (_buttonsTween != null && _buttonsTween.IsActive()) _buttonsTween.Kill(false);
            _buttonsTween = null;
        }

        private void OnDestroy() => KillAll();
    }
}
