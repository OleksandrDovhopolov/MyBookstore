using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Dialogue;
using Game.Tutorial;
using Game.Tutorial.Content;
using Game.UI;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tutorial.Tests.Editor
{
    public sealed class TutorialDialogueStepTests
    {
        [Test]
        public async Task ExecuteAsync_ShowsDialogWindowAndWaitsForClose()
        {
            var ui = new FakeUIManager();
            var window = CreateShownDialogWindow();
            ui.WindowToReturn = window;
            var step = new TutorialDialogueStep("dialogue", ui, TutorialContent.Dialogues.HubIntro);

            var task = step.ExecuteAsync(CancellationToken.None).AsTask();
            await UniTask.Yield();

            Assert.IsFalse(task.IsCompleted);
            Assert.IsInstanceOf<DialogWindowArgs>(ui.LastArgs);
            Assert.AreEqual(TutorialContent.Dialogues.HubIntro, ((DialogWindowArgs)ui.LastArgs).Payload.DialogueId);

            RaiseClosed(window);
            await task;
        }

        [Test]
        public void ExecuteAsync_NullUi_FailOpens()
        {
            var step = new TutorialDialogueStep("dialogue", null, TutorialContent.Dialogues.HubIntro);

            LogAssert.Expect(
                UnityEngine.LogType.Warning,
                $"{TutorialLog.Prefix} dialogue step 'dialogue' skipped: IUIManager is missing.");

            step.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        [Test]
        public void ExecuteAsync_EmptyDialogueId_FailOpens()
        {
            var step = new TutorialDialogueStep("dialogue", new FakeUIManager(), "");

            LogAssert.Expect(
                UnityEngine.LogType.Warning,
                $"{TutorialLog.Prefix} dialogue step 'dialogue' skipped: dialogue id is empty.");

            step.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        [Test]
        public void ExecuteAsync_NullWindow_FailOpens()
        {
            var step = new TutorialDialogueStep("dialogue", new FakeUIManager(), TutorialContent.Dialogues.HubIntro);

            LogAssert.Expect(
                UnityEngine.LogType.Warning,
                $"{TutorialLog.Prefix} dialogue step 'dialogue' skipped: dialogue window was not shown.");

            step.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        [Test]
        public async Task ExecuteAsync_CancellationWhileWaiting_Propagates()
        {
            var ui = new FakeUIManager { WindowToReturn = CreateShownDialogWindow() };
            var step = new TutorialDialogueStep("dialogue", ui, TutorialContent.Dialogues.HubIntro);
            using var cts = new CancellationTokenSource();

            var task = step.ExecuteAsync(cts.Token).AsTask();
            await UniTask.Yield();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(async () => await task);
        }

        private static DialogWindow CreateShownDialogWindow()
        {
            var window = new DialogWindow();
            typeof(WindowController<DialogWindowView>)
                .GetField("<IsShown>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(window, true);
            return window;
        }

        private static void RaiseClosed(DialogWindow window)
        {
            var field = typeof(WindowController<DialogWindowView>)
                .GetField("Closed", BindingFlags.Instance | BindingFlags.NonPublic);
            var handler = (Action<IWindowController>)field?.GetValue(window);
            handler?.Invoke(window);
        }

        private sealed class FakeUIManager : IUIManager
        {
            private readonly LockMonitor _locks = new();

            public event Action<IWindowController> WindowShown;
            public event Action<IWindowController> WindowHidden;
            public DialogWindow WindowToReturn { get; set; }
            public WindowArgs LastArgs { get; private set; }

            public UniTask<T> ShowAsync<T>(WindowArgs args = null, CancellationToken ct = default)
                where T : class, IWindowController, new()
            {
                LastArgs = args;
                return UniTask.FromResult(WindowToReturn as T);
            }

            public UniTask HideAsync<T>(bool forceClose = false, CancellationToken ct = default)
                where T : class, IWindowController
                => UniTask.CompletedTask;

            public UniTask HideAsync(IWindowController controller, bool forceClose = false, CancellationToken ct = default)
                => UniTask.CompletedTask;

            public UniTask HideTopAsync(WindowLayer? layer = null, CancellationToken ct = default)
                => UniTask.CompletedTask;

            public IWindowController GetTopWindow(WindowLayer? layer = null) => null;
            public bool IsWindowShown<T>() where T : class, IWindowController => false;
            public bool IsWindowSpawned<T>() where T : class, IWindowController => false;
            public Game.UI.Lock SetManualLock(object owner) => _locks.Acquire(owner);
        }
    }
}
