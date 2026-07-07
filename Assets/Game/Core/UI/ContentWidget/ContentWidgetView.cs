using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.UI.ContentWidget
{
    public sealed class ContentWidgetView : WindowView
    {
        private const string Tag = "[ContentWidgetView]";

        [SerializeField] private RectTransform _container;
        [SerializeField] private RectTransform _contentContainer;
        [SerializeField] private Button _backdrop;
        [SerializeField] private Button _closeButton;
        [SerializeField] private float _verticalOffset = 12f;
        [SerializeField] private float _edgePadding = 16f;

        private readonly Dictionary<Type, MonoBehaviour> _cachedViews = new();

        private MonoBehaviour _activeView;
        private CancellationTokenSource _showCts;
        private int _showVersion;

        protected override void Awake()
        {
            base.Awake();

            if (_backdrop != null) _backdrop.onClick.AddListener(InvokeCloseEvent);
            if (_closeButton != null) _closeButton.onClick.AddListener(InvokeCloseEvent);
        }

        public void ShowContentView(ContentWidgetDataBase data, RectTransform anchor)
        {
            CancelPendingShow();
            _showCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            ShowContentViewAsync(data, anchor, _showCts.Token, ++_showVersion).Forget();
        }

        public void HideContent()
        {
            CancelPendingShow();
            DeactivateActiveView();
            SetBackdropVisible(false);
        }

        private async UniTaskVoid ShowContentViewAsync(
            ContentWidgetDataBase data,
            RectTransform anchor,
            CancellationToken ct,
            int version)
        {
            try
            {
                SetBackdropVisible(false);
                DeactivateActiveView();

                if (data == null || anchor == null)
                {
                    InvokeCloseEvent();
                    return;
                }

                var prefab = WidgetRegistry.GetPrefab(data.GetType());
                if (prefab == null)
                {
                    Debug.LogWarning($"{Tag} No widget prefab registered for {data.GetType().Name}.");
                    InvokeCloseEvent();
                    return;
                }

                var instance = GetOrCreateViewInstance(data.GetType(), prefab);
                if (instance == null || instance is not IContentWidgetView view)
                {
                    Debug.LogWarning($"{Tag} Widget prefab '{prefab.name}' has no {nameof(IContentWidgetView)}.");
                    InvokeCloseEvent();
                    return;
                }

                instance.gameObject.SetActive(true);
                _activeView = instance;

                if (!view.Setup(data))
                {
                    InvokeCloseEvent();
                    return;
                }

                Reposition(anchor);

                await view.OnViewCreatedAsync(ct);
                if (ct.IsCancellationRequested || version != _showVersion) return;

                Reposition(anchor);
                SetBackdropVisible(true);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} Failed to show widget: {e}");
                InvokeCloseEvent();
            }
        }

        private MonoBehaviour GetOrCreateViewInstance(Type dataType, MonoBehaviour prefab)
        {
            if (_cachedViews.TryGetValue(dataType, out var cached) && cached != null)
                return cached;

            var parent = _contentContainer != null ? _contentContainer : _container;
            if (parent == null) return null;

            var instance = Instantiate(prefab, parent);
            instance.gameObject.SetActive(false);
            _cachedViews[dataType] = instance;
            return instance;
        }

        private void Reposition(RectTransform anchor)
        {
            if (_container == null || anchor == null) return;

            var parent = _container.parent as RectTransform;
            if (parent == null) return;

            NormalizeContainerAnchors(parent);

            if (!TryResolveAnchorRect(anchor, parent, out var anchorRect))
                return;

            var widgetSize = _container.rect.size;
            if (widgetSize.x <= 0f || widgetSize.y <= 0f)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_container);
                Canvas.ForceUpdateCanvases();
                widgetSize = _container.rect.size;
            }

            _container.anchoredPosition = ContentWidgetPlacement.Resolve(
                anchorRect,
                widgetSize,
                parent.rect,
                _verticalOffset,
                _edgePadding,
                _container.pivot,
                out _);
        }

        private void NormalizeContainerAnchors(RectTransform parent)
        {
            var size = _container.rect.size;
            var pointAnchor = parent.pivot;

            _container.anchorMin = pointAnchor;
            _container.anchorMax = pointAnchor;
            _container.sizeDelta = size;
        }

        private static bool TryResolveAnchorRect(
            RectTransform anchor,
            RectTransform parent,
            out Rect rect)
        {
            rect = default;
            if (anchor == null || parent == null) return false;

            var anchorCanvasCamera = ResolveCanvasCamera(anchor);
            var parentCanvasCamera = ResolveCanvasCamera(parent);
            var worldCorners = new Vector3[4];
            anchor.GetWorldCorners(worldCorners);

            var hasPoint = false;
            var min = Vector2.zero;
            var max = Vector2.zero;

            for (var i = 0; i < worldCorners.Length; i++)
            {
                var screenPoint = RectTransformUtility.WorldToScreenPoint(anchorCanvasCamera, worldCorners[i]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        parent,
                        screenPoint,
                        parentCanvasCamera,
                        out var localPoint))
                    continue;

                if (!hasPoint)
                {
                    min = localPoint;
                    max = localPoint;
                    hasPoint = true;
                }
                else
                {
                    min = Vector2.Min(min, localPoint);
                    max = Vector2.Max(max, localPoint);
                }
            }

            if (!hasPoint) return false;

            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        private static Camera ResolveCanvasCamera(RectTransform rectTransform)
        {
            var canvas = rectTransform != null ? rectTransform.GetComponentInParent<Canvas>() : null;
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
        }

        private void DeactivateActiveView()
        {
            if (_activeView != null)
                _activeView.gameObject.SetActive(false);

            _activeView = null;
        }

        private void SetBackdropVisible(bool visible)
        {
            if (_backdrop != null)
                _backdrop.gameObject.SetActive(visible);
        }

        private void CancelPendingShow()
        {
            _showVersion++;
            if (_showCts == null) return;

            _showCts.Cancel();
            _showCts.Dispose();
            _showCts = null;
        }

        protected override void OnDestroy()
        {
            CancelPendingShow();

            if (_backdrop != null) _backdrop.onClick.RemoveListener(InvokeCloseEvent);
            if (_closeButton != null) _closeButton.onClick.RemoveListener(InvokeCloseEvent);

            foreach (var view in _cachedViews.Values)
                if (view != null)
                    Object.Destroy(view.gameObject);

            _cachedViews.Clear();
            base.OnDestroy();
        }
    }
}
