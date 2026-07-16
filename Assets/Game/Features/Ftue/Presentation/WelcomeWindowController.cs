using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Ftue.Domain;
using Game.Ftue.Services;
using Game.UI;
using Save;
using UnityEngine;
using VContainer;

namespace Game.Ftue
{
    /// <summary>
    /// First-entry welcome letter. Flow: show → idle (waits for a click on the letter) → letter-open
    /// clip reveals static text + Start → on Start, persist <c>ftue.welcome_completed</c> and load the
    /// next scene. Shown only on first entry; re-shows on relaunch until Start is pressed (the flag is
    /// written only here, on Start). It does NOT grant rewards — that stays in <c>FtueBootstrapper</c>.
    /// </summary>
    [Window("WelcomeWindow", WindowType.Page)]
    public class WelcomeWindowController : WindowController<WelcomeWindowView>
    {
        private const string LogPrefix = "[FTUE.Welcome]";

        private ISaveService _save;

        [Inject]
        public void Construct(ISaveService save)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
        }

        protected override void OnInit()
        {
            View.LetterClick += OnLetterClicked;
            View.StartClick += OnStartClicked;
        }

        protected override void OnShowStart()
        {
            // Nothing is clickable while the show clip plays.
            View.SetLetterInteractable(false);
            View.SetStartInteractable(false);
        }

        protected override void OnShowComplete()
        {
            base.OnShowComplete();
            // Show finished, animator is now in the idle loop — wait for the letter click (step 3).
            View.SetLetterInteractable(true);
        }

        protected override void OnDispose()
        {
            if (View == null) return;
            View.LetterClick -= OnLetterClicked;
            View.StartClick -= OnStartClicked;
        }

        private void OnLetterClicked() => OpenLetterAsync(View.destroyCancellationToken).Forget();

        private async UniTaskVoid OpenLetterAsync(CancellationToken ct)
        {
            View.SetLetterInteractable(false);   // step 4: the catcher is consumed

            var animation = View.WelcomeAnimation;
            if (animation == null)
            {
                // The prefab's WindowView.Animation field is not a WelcomeWindowAnimation (unassigned
                // or a different WindowAnimation type). Fail loudly and still let the player reach Start
                // so they are not locked in the window.
                Debug.LogError($"{LogPrefix} WindowView.Animation is not a WelcomeWindowAnimation — " +
                               "assign a WelcomeWindowAnimation to the WelcomeWindow prefab's Animation field.");
                View.SetStartInteractable(true);
                return;
            }

            try
            {
                await animation.PlayLetterClickAsync(ct);   // step 5: envelope opens
            }
            catch (OperationCanceledException)
            {
                return;
            }

            View.SetStartInteractable(true);     // Start becomes the only clickable element
        }

        private void OnStartClicked() => StartAsync(View.destroyCancellationToken).Forget();

        private async UniTaskVoid StartAsync(CancellationToken ct)
        {
            View.SetStartInteractable(false);    // guard against a double click

            await WriteWelcomeCompletedAsync(ct);

            try
            {
                await EnterFirstSceneAsync(ct);  // step 6
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                // welcome_completed is already true — fine, it only tracks the letter. Let the player
                // retry the transition; the first-location tutorial is guarded by its own flag.
                Debug.LogError($"{LogPrefix} scene load after Start failed: {e}");
                View.SetStartInteractable(true);
            }
        }

        private UniTask WriteWelcomeCompletedAsync(CancellationToken ct)
        {
            var state = new WelcomeCompletedState
            {
                Completed = true,
                CompletedAtUtcIso = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };
            return _save.UpdateModuleAsync(
                FtueSaveKeys.WelcomeCompleted, state, FtueSaveKeys.WelcomeCompletedSchemaVersion, ct);
        }

        // Start just closes the welcome letter, revealing the scene underneath. What happens next is
        // owned by MainSceneBootstrap: on a fresh day 1 with FirstDayEntryMode.Location it drives the
        // direct entry into the location (auto-stock + additive LocationScene); otherwise it reveals the
        // hub. This window only tracks the welcome letter (ftue.welcome_completed), nothing else.
        private UniTask EnterFirstSceneAsync(CancellationToken ct)
            => CloseAsync(ct);   // return it so StartAsync awaits the close (and its catch can react)
    }
}
