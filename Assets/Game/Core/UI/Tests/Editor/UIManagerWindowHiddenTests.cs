using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Core.UI.Tests.Editor
{
    public sealed class UIManagerWindowHiddenTests
    {
        [Test]
        public void HideAsync_FiresWindowHiddenAfterRemovingFromStack()
        {
            var controller = new FakeWindowController();
            var storage = new FakeStorage(controller);
            var stack = new FakeStack();
            var ui = CreateManager(storage, stack);
            var hiddenCount = 0;
            var stackRemovedBeforeEvent = false;
            ui.WindowHidden += hidden =>
            {
                hiddenCount++;
                stackRemovedBeforeEvent = stack.RemoveCalled;
                Assert.AreSame(controller, hidden);
            };

            ui.HideAsync(controller).GetAwaiter().GetResult();

            Assert.AreEqual(1, hiddenCount);
            Assert.IsTrue(stackRemovedBeforeEvent);
            Assert.IsTrue(controller.HideCalled);
        }

        [Test]
        public void HideAsync_BlockedClose_DoesNotFireWindowHidden()
        {
            var controller = new FakeWindowController { IsCloseBlockedValue = true };
            var storage = new FakeStorage(controller);
            var stack = new FakeStack();
            var ui = CreateManager(storage, stack);
            var hiddenCount = 0;
            ui.WindowHidden += _ => hiddenCount++;

            ui.HideAsync(controller, forceClose: false).GetAwaiter().GetResult();

            Assert.AreEqual(0, hiddenCount);
            Assert.IsFalse(stack.RemoveCalled);
            Assert.IsFalse(controller.HideCalled);
        }

        private static UIManager CreateManager(FakeStorage storage, FakeStack stack)
            => new(
                canvasRoot: null,
                factory: new FakeFactory(),
                stack: stack,
                sorting: new FakeSorting(),
                storage: storage,
                filter: new FakeFilter(),
                locks: new LockMonitor());

        private sealed class FakeWindowController : IWindowController
        {
            public WindowAttribute Attribute { get; } = new("Fake", WindowType.Popup, keepInCache: true);
            public WindowArgs Arguments => null;
            public IWindow View => null;
            public bool IsShown => true;
            public bool IsCloseBlocked => IsCloseBlockedValue;
            public bool IsCloseBlockedValue { get; set; }
            public bool HideCalled { get; private set; }
            public event Action<IWindowController> Closed;
            public void Configure(WindowView view, WindowAttribute attribute) { }
            public void ApplyArguments(WindowArgs args) { }
            public UniTask ShowAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask HideAsync(bool isClosed, CancellationToken ct)
            {
                HideCalled = true;
                if (isClosed) Closed?.Invoke(this);
                return UniTask.CompletedTask;
            }

            public void SetHudVisible(bool visible) { }
            public void Dispose() { }
        }

        private sealed class FakeStorage : IUIStorage
        {
            private readonly List<IWindowController> _all;

            public FakeStorage(IWindowController controller)
            {
                _all = new List<IWindowController> { controller };
            }

            public IReadOnlyCollection<IWindowController> All => _all;
            public bool TryGet(Type controllerType, int primaryKey, out IWindowController controller)
            {
                controller = null;
                return false;
            }

            public void Add(IWindowController controller, int primaryKey) => _all.Add(controller);
            public bool Remove(IWindowController controller) => _all.Remove(controller);
            public void Clear() => _all.Clear();
        }

        private sealed class FakeStack : IUIStack
        {
            public bool RemoveCalled { get; private set; }
            public void Push(IWindowController controller, WindowLayer layer) { }
            public bool Remove(IWindowController controller)
            {
                RemoveCalled = true;
                return true;
            }

            public IWindowController GetTop(WindowLayer layer) => null;
            public IWindowController GetFocused() => null;
            public WindowLayer? GetLayerOf(IWindowController controller) => null;
            public IReadOnlyList<IWindowController> GetAll(WindowLayer layer) => Array.Empty<IWindowController>();
        }

        private sealed class FakeSorting : IUISortingController
        {
            public void Apply(IWindowController controller, WindowLayer layer) { }
            public void Release(IWindowController controller) { }
        }

        private sealed class FakeFilter : IUiFilter
        {
            public bool CanBeShown(Type controllerType) => true;
            public void AddFilter(IUiWindowFilter filter) { }
            public void RemoveFilter(IUiWindowFilter filter) { }
        }

        private sealed class FakeFactory : IWindowFactory
        {
            public UniTask<T> CreateAsync<T>(Transform parent, CancellationToken ct)
                where T : class, IWindowController, new()
                => UniTask.FromResult<T>(null);

            public void Destroy(IWindowController controller) { }
        }
    }
}
