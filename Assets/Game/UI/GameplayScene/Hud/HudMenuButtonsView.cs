using System;
using Cysharp.Threading.Tasks;
using Game.Decor.UI;
using Game.Inventory.UI;
using Game.Journal.UI;
using Game.Shop.UI;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GameplayUI
{
    public sealed class HudMenuButtonsView : MonoBehaviour
    {
        [SerializeField] private Button _decorButton;
        [SerializeField] private Button _journalButton;
        [SerializeField] private Button _inventoryButton;
        [SerializeField] private Button _shopButton;

        private IHudWindowLauncher _launcher;
        private bool _opening;

        public void Bind(IHudWindowLauncher launcher)
        {
            Unbind();
            _launcher = launcher;

            if (_decorButton != null)
                _decorButton.onClick.AddListener(OnDecorButtonClicked);

            if (_journalButton != null)
                _journalButton.onClick.AddListener(OnJournalButtonClicked);

            if (_inventoryButton != null)
                _inventoryButton.onClick.AddListener(OnInventoryButtonClicked);

            if (_shopButton != null)
                _shopButton.onClick.AddListener(OnShopButtonClicked);
        }

        public void Unbind()
        {
            if (_decorButton != null)
                _decorButton.onClick.RemoveListener(OnDecorButtonClicked);

            if (_journalButton != null)
                _journalButton.onClick.RemoveListener(OnJournalButtonClicked);

            if (_inventoryButton != null)
                _inventoryButton.onClick.RemoveListener(OnInventoryButtonClicked);

            if (_shopButton != null)
                _shopButton.onClick.RemoveListener(OnShopButtonClicked);

            _launcher = null;
            _opening = false;
        }

        public void SetInteractable(bool value)
        {
            SetButtonInteractable(_decorButton, value);
            SetButtonInteractable(_journalButton, value);
            SetButtonInteractable(_inventoryButton, value);
            SetButtonInteractable(_shopButton, value);
        }

        private void OnDecorButtonClicked() => OpenAsync<DecorPlacementWindow>().Forget();
        private void OnJournalButtonClicked() => OpenAsync<JournalWindow>().Forget();
        private void OnInventoryButtonClicked() => OpenAsync<InventoryWindowController>().Forget();
        private void OnShopButtonClicked() => OpenAsync<ShopWindow>().Forget();

        private async UniTaskVoid OpenAsync<TWindow>()
            where TWindow : class, IWindowController, new()
        {
            if (_opening || _launcher == null) return;

            _opening = true;
            try
            {
                await _launcher.OpenAsync<TWindow>();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogError($"[HudMenuButtonsView] Failed to open {typeof(TWindow).Name}: {e}");
            }
            finally
            {
                _opening = false;
            }
        }

        private static void SetButtonInteractable(Button button, bool value)
        {
            if (button == null) return;

            button.interactable = value;
            button.gameObject.SetActive(value);
        }
    }
}
