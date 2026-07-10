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
        [SerializeField] private float _verticalOffset = 12f;
        [SerializeField] private float _horizontalOffset = 12f;
        [SerializeField] private float _edgePadding = 16f;
        [SerializeField, Range(0f, 0.5f)] private float _verticalZoneRatio = 0.33f;
        [SerializeField] private float _autoCloseDelaySeconds = 5f;

        private readonly Dictionary<Type, MonoBehaviour> _cachedViews = new();

        private MonoBehaviour _activeView;
        private CancellationTokenSource _showCts;
        private int _showVersion;

        public void ShowContentView(ContentWidgetDataBase data, RectTransform anchor)
        {
            CancelPendingShow();
            _showCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            ShowContentViewAsync(data, anchor, _showCts.Token, ++_showVersion).Forget();
        }

        public void RequestClose()
        {
            CancelPendingShow();
            InvokeCloseEvent();
        }

        public void HideContent()
        {
            CancelPendingShow();
            DeactivateActiveView();
        }

        private async UniTaskVoid ShowContentViewAsync(
            ContentWidgetDataBase data,
            RectTransform anchor,
            CancellationToken ct,
            int version)
        {
            try
            {
                DeactivateActiveView();

                if (data == null || anchor == null)
                {
                    RequestClose();
                    return;
                }

                var prefab = WidgetRegistry.GetPrefab(data.GetType());
                if (prefab == null)
                {
                    Debug.LogWarning($"{Tag} No widget prefab registered for {data.GetType().Name}.");
                    RequestClose();
                    return;
                }

                var instance = GetOrCreateViewInstance(data.GetType(), prefab);
                if (instance == null || instance is not IContentWidgetView view)
                {
                    Debug.LogWarning($"{Tag} Widget prefab '{prefab.name}' has no {nameof(IContentWidgetView)}.");
                    RequestClose();
                    return;
                }

                instance.gameObject.SetActive(true);
                _activeView = instance;

                if (!view.Setup(data))
                {
                    RequestClose();
                    return;
                }

                Reposition(anchor);

                await view.OnViewCreatedAsync(ct);
                if (ct.IsCancellationRequested || version != _showVersion) return;

                Reposition(anchor);
                StartAutoCloseTimer(ct, version);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} Failed to show widget: {e}");
                RequestClose();
            }
        }

        private void StartAutoCloseTimer(CancellationToken ct, int version)
        {
            if (_autoCloseDelaySeconds <= 0f) return;

            AutoCloseAsync(ct, version).Forget();
        }

        private async UniTaskVoid AutoCloseAsync(CancellationToken ct, int version)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_autoCloseDelaySeconds), cancellationToken: ct);
                if (ct.IsCancellationRequested || version != _showVersion) return;

                RequestClose();
            }
            catch (OperationCanceledException)
            {
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

            var placement = ContentWidgetPlacement.Resolve(
                anchorRect,
                widgetSize,
                parent.rect,
                _verticalOffset,
                _horizontalOffset,
                _edgePadding,
                _verticalZoneRatio,
                _container.pivot);

            _container.anchoredPosition = placement.Position;
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

            foreach (var view in _cachedViews.Values)
                if (view != null)
                    DestroyCachedView(view);

            _cachedViews.Clear();
            base.OnDestroy();
        }

        private static void DestroyCachedView(MonoBehaviour view)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(view.gameObject);
                return;
            }
#endif
            Object.Destroy(view.gameObject);
        }
    }
}
