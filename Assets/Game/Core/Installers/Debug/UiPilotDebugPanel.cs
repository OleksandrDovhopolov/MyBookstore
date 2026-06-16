#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Cysharp.Threading.Tasks;
using Game.Decor;
using Game.Decor.UI;
using Game.Inventory.UI;
using Game.UI.Common;
using UnityEngine;
using VContainer;

namespace Game.UI.DebugPanel
{
    // Editor / dev-build debug overlay. Lives on the UIManagerCanvas prefab root next to UICanvasRoot.
    // Press the on-screen buttons to verify Phase 1 windows manually.
    public sealed class UiPilotDebugPanel : MonoBehaviour
    {
        private IUIManager _uiManager;
        private IDecorRewardService  _decorReward;

        [Inject]
        public void Construct(IUIManager uiManager, IDecorRewardService  decorReward)
        {
            _uiManager = uiManager;
            _decorReward = decorReward;
        }

        private void OnGUI()
        {
            if (_uiManager == null) return;

            const float w = 200f;
            const float h = 36f;
            const float pad = 8f;

            var y = pad;
            if (GUI.Button(new Rect(pad, y, w, h), "Show Settings"))
            {
                _uiManager.ShowAsync<SettingsWindow>().Forget();
            }

            y += h + pad;
            if (GUI.Button(new Rect(pad, y, w, h), "Show Confirm (await)"))
            {
                ShowStandaloneConfirmAsync().Forget();
            }

            y += h + pad;
            if (GUI.Button(new Rect(pad, y, w, h), "Hide Top"))
            {
                //TODO bug ? tgis button hides GameplaySceneController. but this is main screen hud and cant be hided 
                _uiManager.HideTopAsync().Forget();
            }

            y += h + pad;
            if (GUI.Button(new Rect(pad, y, w, h), "Show Inventory"))
            {
                _uiManager.ShowAsync<InventoryWindowController>().Forget();
            }

            y += h + pad;
            if (GUI.Button(new Rect(pad, y, w, h), "Show Decoration"))
            {
                _uiManager.ShowAsync<DecorPlacementWindow>().Forget();
            }

            y += h + pad;
            if (GUI.Button(new Rect(pad, y, w, h), "Get debug free decor"))
            {
                _decorReward.ClaimFreeDecorAsync(default).Forget();
            }
        }

        private async UniTask ShowStandaloneConfirmAsync()
        {
            var args = new ConfirmDialogArgs(
                title: "Debug Confirm",
                body: "Standalone confirm with no parent. Pick Yes or No.",
                confirmLabel: "Yes",
                cancelLabel: "No");

            var dialog = await _uiManager.ShowAsync<ConfirmDialog>(args);
            if (dialog == null) return;

            var result = await dialog.WaitForResultAsync<ConfirmDialogResult>();
            UnityEngine.Debug.Log($"[UiPilotDebugPanel] Confirm result: {result}");
        }
    }
}
#endif
