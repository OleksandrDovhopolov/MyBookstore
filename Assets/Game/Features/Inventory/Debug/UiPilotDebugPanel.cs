#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Cysharp.Threading.Tasks;
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

        [Inject]
        public void Construct(IUIManager uiManager) => _uiManager = uiManager;

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
                _uiManager.HideTopAsync().Forget();
            }

            y += h + pad;
            if (GUI.Button(new Rect(pad, y, w, h), "Show Inventory"))
            {
                _uiManager.ShowAsync<InventoryWindowController>().Forget();
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
