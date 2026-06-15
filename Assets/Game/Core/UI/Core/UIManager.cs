using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.UI
{
    public sealed class UIManager : IUIManager, IDisposable
    {
        private const string Tag = "[UIManager]";

        private readonly IUICanvasRoot _canvasRoot;
        private readonly IWindowFactory _factory;
        private readonly IUIStack _stack;
        private readonly IUISortingController _sorting;
        private readonly IUIStorage _storage;
        private readonly IUiFilter _filter;
        private readonly LockMonitor _locks;

        private readonly SemaphoreSlim _gate = new(1, 1);

        public UIManager(
            IUICanvasRoot canvasRoot,
            IWindowFactory factory,
            IUIStack stack,
            IUISortingController sorting,
            IUIStorage storage,
            IUiFilter filter,
            LockMonitor locks)
        {
            _canvasRoot = canvasRoot;
            _factory = factory;
            _stack = stack;
            _sorting = sorting;
            _storage = storage;
            _filter = filter;
            _locks = locks;

            _locks.StateChanged += OnLocksChanged;
        }

        public async UniTask<T> ShowAsync<T>(WindowArgs args = null, CancellationToken ct = default)
            where T : class, IWindowController, new()
        {
            if (!_filter.CanBeShown(typeof(T)))
            {
                Debug.Log($"{Tag} {typeof(T).Name} blocked by filter.");
                return null;
            }

            await _gate.WaitAsync(ct);
            try
            {
                var attribute = typeof(T).GetCustomAttribute<WindowAttribute>()
                    ?? throw new InvalidOperationException(
                        $"{Tag} {typeof(T).Name} missing [Window] attribute.");

                var layer = args?.LayerOverride ?? DefaultLayerFor(attribute.Type);
                var parent = ResolveParent(attribute.Type);
                var primaryKey = args?.GetPrimaryKey() ?? 0;

                T controller;
                if (_storage.TryGet(typeof(T), primaryKey, out var existing) && existing is T cached)
                {
                    controller = cached;
                    cached.View.RectTransform.SetParent(parent, false);
                }
                else
                {
                    controller = await _factory.CreateAsync<T>(parent, ct);
                    _storage.Add(controller, primaryKey);
                }

                controller.ApplyArguments(args);

                // Activate the view + zero alpha BEFORE sorting. Canvas silently drops
                // sortingLayerName changes while its GameObject is inactive (Unity quirk:
                // the Canvas isn't registered with the renderer until enabled).
                controller.View.CanvasGroup.alpha = 0f;
                controller.View.GameObject.SetActive(true);

                _sorting.Apply(controller, layer);
                _stack.Push(controller, layer);

                using (_locks.Acquire(controller))
                {
                    await controller.ShowAsync(ct);
                }

                return controller;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async UniTask HideAsync<T>(bool forceClose = false, CancellationToken ct = default)
            where T : class, IWindowController
        {
            await _gate.WaitAsync(ct);
            try
            {
                var top = _storage.All.OfType<T>().LastOrDefault(c => c.IsShown);
                if (top == null) return;
                await HideInternalAsync(top, forceClose, ct);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async UniTask HideAsync(IWindowController controller, bool forceClose = false, CancellationToken ct = default)
        {
            if (controller == null) return;
            await _gate.WaitAsync(ct);
            try
            {
                if (!_storage.All.Contains(controller)) return;
                await HideInternalAsync(controller, forceClose, ct);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async UniTask HideTopAsync(WindowLayer? layer = null, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);
            try
            {
                var target = layer.HasValue ? _stack.GetTop(layer.Value) : _stack.GetFocused();
                if (target == null) return;
                await HideInternalAsync(target, forceClose: true, ct);
            }
            finally
            {
                _gate.Release();
            }
        }

        public IWindowController GetTopWindow(WindowLayer? layer = null)
            => layer.HasValue ? _stack.GetTop(layer.Value) : _stack.GetFocused();

        public bool IsWindowShown<T>() where T : class, IWindowController
            => _storage.All.OfType<T>().Any(c => c.IsShown);

        public bool IsWindowSpawned<T>() where T : class, IWindowController
            => _storage.All.OfType<T>().Any();

        public Lock SetManualLock(object owner) => _locks.Acquire(owner);

        public void Dispose()
        {
            _locks.StateChanged -= OnLocksChanged;
            _gate.Dispose();
        }

        private async UniTask HideInternalAsync(IWindowController controller, bool forceClose, CancellationToken ct)
        {
            if (controller.IsCloseBlocked && !forceClose) return;

            // Close children (typically Additional popups whose ParentWindow == this controller)
            // before closing the parent. Snapshot first since HideInternalAsync mutates _storage.
            var children = _storage.All
                .Where(c => c != controller && c.Arguments?.ParentWindow == controller)
                .ToList();
            foreach (var child in children)
            {
                await HideInternalAsync(child, forceClose: true, ct);
            }

            using (_locks.Acquire(controller))
            {
                await controller.HideAsync(isClosed: true, ct);
            }

            _stack.Remove(controller);
            _sorting.Release(controller);

            if (!controller.Attribute.KeepInCache)
            {
                _storage.Remove(controller);
                _factory.Destroy(controller);
            }
        }

        private Transform ResolveParent(WindowType type)
            => type == WindowType.HUD ? _canvasRoot.HudRoot : _canvasRoot.WindowsRoot;

        private static WindowLayer DefaultLayerFor(WindowType type) => type switch
        {
            WindowType.Page => WindowLayer.Main,
            WindowType.Popup => WindowLayer.Additional,
            WindowType.Widget => WindowLayer.Main,
            _ => WindowLayer.Main,
        };

        private void OnLocksChanged()
        {
            if (_canvasRoot?.Blocker != null)
            {
                _canvasRoot.Blocker.SetActive(_locks.HasAnyLock);
            }
        }
    }
}
