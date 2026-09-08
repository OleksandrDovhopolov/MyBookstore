using System;
using Cysharp.Threading.Tasks;
using Game.Decor.UI;
using Game.Inventory.UI;
using Game.Journal.UI;
using Game.Shop.UI;
using Game.UI;
using Infrastructure.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace GameplayUI
{
    public sealed class HudMenuButtonsView : MonoBehaviour
    {
        [SerializeField] private Button _cheatButton;
        [SerializeField] private Button _startDayButton;
        [SerializeField] private Button _decorButton;
        [SerializeField] private Button _journalButton;
        [SerializeField] private Button _inventoryButton;
        [SerializeField] private Button _shopButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private GameObject _journalBadge;

        private IHudWindowLauncher _launcher;
        private bool _opening;
        private bool _journalBadgeInitialized;
        private bool _journalBadgeOn;

        public event Action StartDayClicked;

        public void Bind(IHudWindowLauncher launcher)
        {
            Unbind();
            _launcher = launcher;

            if (_startDayButton != null)
                _startDayButton.onClick.AddListener(OnStartDayButtonClicked);

            if (_decorButton != null)
                _decorButton.onClick.AddListener(OnDecorButtonClicked);

            if (_journalButton != null)
                _journalButton.onClick.AddListener(OnJournalButtonClicked);

            if (_inventoryButton != null)
                _inventoryButton.onClick.AddListener(OnInventoryButtonClicked);

            if (_shopButton != null)
                _shopButton.onClick.AddListener(OnShopButtonClicked);

            if (_settingsButton != null)
                _settingsButton.onClick.AddListener(OnSettingsButtonClicked);
        }

        public void Unbind()
        {
            if (_startDayButton != null)
                _startDayButton.onClick.RemoveListener(OnStartDayButtonClicked);

            if (_decorButton != null)
                _decorButton.onClick.RemoveListener(OnDecorButtonClicked);

            if (_journalButton != null)
                _journalButton.onClick.RemoveListener(OnJournalButtonClicked);

            if (_inventoryButton != null)
                _inventoryButton.onClick.RemoveListener(OnInventoryButtonClicked);

            if (_shopButton != null)
                _shopButton.onClick.RemoveListener(OnShopButtonClicked);

            if (_settingsButton != null)
                _settingsButton.onClick.RemoveListener(OnSettingsButtonClicked);

            _launcher = null;
            _opening = false;
        }

        public void SetInteractable(bool value)
        {
            SetButtonInteractable(_cheatButton, value);
            SetStartButtonActive(value);
            SetButtonInteractable(_decorButton, value);
            SetButtonInteractable(_journalButton, value);
            SetButtonInteractable(_inventoryButton, value);
            SetButtonInteractable(_shopButton, value);
            SetButtonInteractable(_settingsButton, value);
        }

        public void SetStartButtonActive(bool active) => SetButtonInteractable(_startDayButton, active);

        public void SetJournalBadge(bool on)
        {
            if (_journalBadge != null)
                _journalBadge.SetActive(on);

            if (!_journalBadgeInitialized)
            {
                _journalBadgeInitialized = true;
                _journalBadgeOn = on;
                return;
            }

            if (!_journalBadgeOn && on)
                PlayUi(Audio.Catalog?.NewJournalEntry);

            _journalBadgeOn = on;
        }

        private void OnStartDayButtonClicked() => StartDayClicked?.Invoke();
        private void OnDecorButtonClicked() => OpenAsync<DecorPlacementWindow>().Forget();
        private void OnJournalButtonClicked() => OpenAsync<JournalWindow>().Forget();
        private void OnInventoryButtonClicked() => OpenAsync<InventoryWindowController>().Forget();
        private void OnShopButtonClicked() => OpenAsync<ShopWindow>().Forget();
        private void OnSettingsButtonClicked() => OpenAsync<SettingsWindowController>().Forget();

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

        private static void PlayUi(AudioClip clip)
        {
            if (clip != null) Audio.PlayUi(clip);
        }
    }
}
