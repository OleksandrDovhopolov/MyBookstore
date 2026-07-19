using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI;
using Infrastructure.TutorialUI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tutorial.Presentation
{
    /// <summary>
    /// Owns the tutorial overlay: a runtime Canvas (sortingOrder from settings) created under the persistent
    /// UI canvas root. Hosts the blackout, hit-area, pointer and text panel, and exposes await-until-advance
    /// calls for the step handlers. Visual only; the tutorial state machine never references this.
    /// </summary>
    public sealed class TutorialOverlayController
    {
        private const string LogPrefix = "[Tutorial]";

        private readonly IUICanvasRoot _canvasRoot;
        private readonly TutorialOverlaySettings _settings;

        private RectTransform _root;
        private TutorialBlackoutView _blackout;
        private TutorialHitAreaView _hitArea;
        private TutorialPointerView _pointer;
        private TutorialTextPanelView _textPanel;

        public TutorialOverlayController(IUICanvasRoot canvasRoot, TutorialOverlaySettings settings)
        {
            _canvasRoot = canvasRoot;
            _settings = settings != null ? settings : TutorialOverlaySettings.CreateDefault();
        }

        public async UniTask ShowTextAndWaitTapAsync(string text, string placement, CancellationToken ct)
        {
            if (!EnsureRoot()) return;

            _blackout.ShowFullCover();
            _hitArea?.HideView();
            _textPanel?.SetText(text, placement);
            _pointer?.HideView();

            var tcs = new UniTaskCompletionSource();
            void OnTap() => tcs.TrySetResult();
            _blackout.Tapped += OnTap;
            try
            {
                await tcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _blackout.Tapped -= OnTap;
            }
        }

        public void HideText()
        {
            _blackout?.HideView();
            _hitArea?.HideView();
            _textPanel?.HideView();
        }

        public void HideBlackout()
        {
            _blackout?.HideView();
        }

        public void ShowCallout(string text, string placement)
        {
            if (!EnsureRoot()) return;

            _textPanel?.SetText(text, placement);
            _pointer?.HideView();
        }

        public void HideCallout()
        {
            _textPanel?.HideView();
        }

        public async UniTask HighlightAndWaitClickAsync(
            RectTransform target, string text, string placement, bool pointer, CancellationToken ct)
        {
            if (!EnsureRoot()) return;
            if (target == null)
            {
                Debug.LogWarning($"{LogPrefix} highlight target is missing; auto-advancing.");
                return;
            }

            _blackout.ShowWithHole(target, _settings.HolePadding);
            _hitArea?.HideView();
            _textPanel?.SetText(text, placement);
            if (pointer) _pointer?.PointAt(target); else _pointer?.HideView();

            var button = target != null ? target.GetComponent<Button>() : null;
            if (button == null)
                await WaitForHitAreaClickAsync(target, ct);
            else
                await WaitForButtonClickAsync(target, button, ct);
        }

        private async UniTask WaitForButtonClickAsync(RectTransform target, Button button, CancellationToken ct)
        {
            var tcs = new UniTaskCompletionSource();
            void OnClick() => tcs.TrySetResult();
            button.onClick.AddListener(OnClick);
            try
            {
                using var registration = ct.Register(() => tcs.TrySetCanceled(ct));
                // Race the click against the target being lost so a vanished button never soft-locks the tutorial.
                var lost = UniTask.WaitUntil(() => IsTargetLost(target, button), cancellationToken: ct);
                var winIndex = await UniTask.WhenAny(tcs.Task, lost);
                ct.ThrowIfCancellationRequested();
                if (winIndex == 1)
                    Debug.LogWarning($"{LogPrefix} highlight target lost before click; auto-advancing.");
            }
            finally
            {
                button.onClick.RemoveListener(OnClick);
            }
        }

        private async UniTask WaitForHitAreaClickAsync(RectTransform target, CancellationToken ct)
        {
            var tcs = new UniTaskCompletionSource();
            void OnClick() => tcs.TrySetResult();

            _hitArea.PointAt(target, _settings.HolePadding);
            _hitArea.Clicked += OnClick;
            try
            {
                using var registration = ct.Register(() => tcs.TrySetCanceled(ct));
                var lost = UniTask.WaitUntil(
                    () => target == null || !target.gameObject.activeInHierarchy,
                    cancellationToken: ct);
                var winIndex = await UniTask.WhenAny(tcs.Task, lost);
                ct.ThrowIfCancellationRequested();
                if (winIndex == 1)
                    Debug.LogWarning($"{LogPrefix} highlight target lost before click; auto-advancing.");
            }
            finally
            {
                _hitArea.Clicked -= OnClick;
                _hitArea.HideView();
            }
        }

        private static bool IsTargetLost(RectTransform target, Button button)
            => target == null
               || !target.gameObject.activeInHierarchy
               || (button != null && !button.interactable);

        public void HideHighlight()
        {
            _blackout?.HideView();
            _hitArea?.HideView();
            _textPanel?.HideView();
            _pointer?.HideView();
        }

        public void Hide()
        {
            _blackout?.HideView();
            _hitArea?.HideView();
            _pointer?.HideView();
            _textPanel?.HideView();
        }

        private bool EnsureRoot()
        {
            if (_root != null) return true;

            var parent = _canvasRoot?.WindowsRoot != null ? _canvasRoot.WindowsRoot : _canvasRoot?.HudRoot;
            if (parent == null)
            {
                Debug.LogWarning($"{LogPrefix} UI canvas root unavailable - overlay cannot be created.");
                return false;
            }

            var go = new GameObject("TutorialOverlayRoot",
                typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);

            _root = (RectTransform)go.transform;
            Stretch(_root);

            var canvas = go.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = _settings.SortingOrder;

            _blackout = CreateStretchedChild<TutorialBlackoutView>("Blackout");
            _blackout.Configure(_settings.BlackoutColor);
            _blackout.HideView();

            var hitAreaGo = new GameObject("HitArea", typeof(RectTransform), typeof(Image), typeof(TutorialHitAreaView));
            var hitAreaRt = (RectTransform)hitAreaGo.transform;
            hitAreaRt.SetParent(_root, false);
            _hitArea = hitAreaGo.GetComponent<TutorialHitAreaView>();
            _hitArea.HideView();

            var pointerGo = new GameObject("Pointer", typeof(RectTransform), typeof(Image), typeof(TutorialPointerView));
            var prt = (RectTransform)pointerGo.transform;
            prt.SetParent(_root, false);
            prt.sizeDelta = new Vector2(64f, 64f);
            _pointer = pointerGo.GetComponent<TutorialPointerView>();
            _pointer.Configure(_settings.PointerSprite, _settings.PointerBounceAmplitude, _settings.PointerBounceSpeed);
            _pointer.HideView();

            if (_settings.TextPanelPrefab != null)
            {
                _textPanel = Object.Instantiate(_settings.TextPanelPrefab, _root);
                _textPanel.HideView();
            }

            return true;
        }

        private T CreateStretchedChild<T>(string name) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(T));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_root, false);
            Stretch(rt);
            return go.GetComponent<T>();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }
    }
}
