using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace Game.UI
{
    public abstract class WindowController<TView> : IWindowController where TView : WindowView
    {
        private bool _isInitialized;

        protected TView View { get; private set; }
        protected IUIManager UIManager { get; private set; }
        public WindowAttribute Attribute { get; private set; }
        public WindowArgs Arguments { get; private set; }
        public bool IsShown { get; private set; }
        public virtual bool IsCloseBlocked => false;

        IWindow IWindowController.View => View;

        public event Action<IWindowController> Closed;

        [Inject]
        public void InjectUIManager(IUIManager uiManager) => UIManager = uiManager;

        protected UniTask CloseAsync(CancellationToken ct = default)
            => UIManager.HideAsync(this, forceClose: false, ct);

        public void Configure(WindowView view, WindowAttribute attribute)
        {
            if (view is not TView typedView)
            {
                throw new InvalidOperationException(
                    $"[{GetType().Name}] expected view of type {typeof(TView).Name}, got {view.GetType().Name}.");
            }

            View = typedView;
            Attribute = attribute;
        }

        public void ApplyArguments(WindowArgs args)
        {
            Arguments = args;
        }

        public async UniTask ShowAsync(CancellationToken ct)
        {
            if (!_isInitialized)
            {
                OnInit();
                _isInitialized = true;
            }
            else
            {
                UpdateWindow();
            }

            OnShowStart();
            // View is already activated and alpha-zeroed by UIManager before sorting.
            // We only run the appearance animation (or snap to full alpha if no animation).

            if (View.Animation != null)
            {
                await View.Animation.PlayInAsync(ct);
            }
            else
            {
                View.CanvasGroup.alpha = 1f;
            }

            IsShown = true;
            OnShowComplete();
        }

        public async UniTask HideAsync(bool isClosed, CancellationToken ct)
        {
            OnHideStart(isClosed);

            if (View.Animation != null)
            {
                await View.Animation.PlayOutAsync(ct);
            }
            else
            {
                View.CanvasGroup.alpha = 0f;
            }

            View.GameObject.SetActive(false);
            IsShown = false;
            OnHideComplete(isClosed);

            if (isClosed)
            {
                Closed?.Invoke(this);
            }
        }

        public void Dispose()
        {
            OnDispose();
            Closed = null;
        }

        protected virtual void OnInit() { }
        protected virtual void OnShowStart() { }
        protected virtual void OnShowComplete() { }
        protected virtual void UpdateWindow() { }
        protected virtual void OnHideStart(bool isClosed) { }
        protected virtual void OnHideComplete(bool isClosed) { }
        protected virtual void OnDispose() { }
    }
}
