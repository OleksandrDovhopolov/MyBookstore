using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.Newspaper.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Decor.UI
{
    /// <summary>
    /// A single decor card in the bottom inventory panel of <see cref="DecorPlacementWindow"/>.
    /// Visual replacement for the debug-only <see cref="DecorInventoryRowView"/>. States:
    /// Available (clickable to select) / Selected (highlighted) / Placed (badge shown, select
    /// disabled — the decor is already in a slot). Info button is always active.
    /// The icon is loaded by decor id via <see cref="IUiSpriteProvider"/> (not IconAddress).
    /// </summary>
    public sealed class DecorInventoryCardView : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _nameLabel;

        [Header("Interaction")]
        [SerializeField] private Button _selectButton;
        [SerializeField] private Button _infoButton;
        [SerializeField] private GameObject _selectedHighlight; // border/glow shown in the Selected state
        [SerializeField] private GameObject _placedBadge;       // "already placed" marker

        private Action<string> _onSelect;
        private Action<string> _onInfo;
        private CancellationTokenSource _iconCts;

        public string DecorId { get; private set; }

        private void Awake()
        {
            if (_selectButton != null) _selectButton.onClick.AddListener(OnSelectClicked);
            if (_infoButton != null) _infoButton.onClick.AddListener(OnInfoClicked);
        }

        public void Bind(DecorConfig config, bool isPlaced, IUiSpriteProvider sprites, Action<string> onSelect, Action<string> onInfo)
        {
            DecorId = config.Id;
            _onSelect = onSelect;
            _onInfo = onInfo;

            if (_nameLabel != null) _nameLabel.text = config.DisplayName ?? config.Id;

            // A placed decor cannot be placed again (domain returns AlreadyPlaced), so it is not
            // selectable — only its badge and info are shown.
            if (_placedBadge != null) _placedBadge.SetActive(isPlaced);
            if (_selectButton != null) _selectButton.interactable = !isPlaced;

            SetSelected(false);
            LoadIcon(config.Id, sprites);
        }

        public void SetSelected(bool selected)
        {
            if (_selectedHighlight != null) _selectedHighlight.SetActive(selected);
        }

        private void LoadIcon(string decorId, IUiSpriteProvider sprites)
        {
            CancelIconLoad();
            if (sprites == null || _icon == null || string.IsNullOrEmpty(decorId)) return;

            _iconCts = new CancellationTokenSource();
            LoadIconAsync(decorId, sprites, _iconCts.Token).Forget();
        }

        private async UniTaskVoid LoadIconAsync(string decorId, IUiSpriteProvider sprites, CancellationToken ct)
        {
            try
            {
                var sprite = await sprites.GetSpriteAsync(decorId, ct);
                if (ct.IsCancellationRequested) return;
                if (_icon != null) _icon.sprite = sprite;
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void OnSelectClicked() => _onSelect?.Invoke(DecorId);

        private void OnInfoClicked() => _onInfo?.Invoke(DecorId);

        private void CancelIconLoad()
        {
            if (_iconCts == null) return;
            _iconCts.Cancel();
            _iconCts.Dispose();
            _iconCts = null;
        }

        private void OnDestroy()
        {
            CancelIconLoad();
            if (_selectButton != null) _selectButton.onClick.RemoveListener(OnSelectClicked);
            if (_infoButton != null) _infoButton.onClick.RemoveListener(OnInfoClicked);
        }
    }
}
