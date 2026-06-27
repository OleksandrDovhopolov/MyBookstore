using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Newspaper.UI;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Location.UI
{
    public sealed class LocationRowView : MonoBehaviour, ICleanup
    {
        [SerializeField] private Image _locationImage;
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private GameObject _lockedPanel;
        [SerializeField] private UIListPool<LocationConditionItemView> _conditionsPool = new();
        [SerializeField] private TextMeshProUGUI _entryCostLabel;
        [SerializeField] private Button _startButton;

        private Action<string> _onStart;
        private string _locationId;
        private CancellationTokenSource _iconCts;

        private void Awake()
        {
            if (_startButton != null)
                _startButton.onClick.AddListener(() => _onStart?.Invoke(_locationId));
        }

        public void Bind(LocationListItemModel model, Action<string> onStart, IUiSpriteProvider sprites)
        {
            _onStart = onStart;
            _locationId = model.LocationId;

            if (_nameLabel != null) _nameLabel.text = model.DisplayName;
            if (_entryCostLabel != null) _entryCostLabel.text = $"{model.EntryCost} {model.EntryCurrencyId}";

            if (_startButton != null) _startButton.interactable = model.StartEnabled;
            if (_lockedPanel != null) _lockedPanel.SetActive(!model.IsUnlocked);

            RenderConditions(model.Conditions);
            LoadIcons(model.LocationId, sprites);
        }

        private void RenderConditions(IReadOnlyList<LocationConditionProgress> conditions)
        {
            _conditionsPool.DisableAll();

            if (conditions != null)
            {
                for (var i = 0; i < conditions.Count; i++)
                    _conditionsPool.GetNext().Bind(conditions[i]);
            }

            _conditionsPool.DisableNonActive();
        }

        // Location art (by location id) and genre chip icons come from Addressables (async); pull them off
        // the shared cache and push into the views under one cancellation token.
        private void LoadIcons(string locationId, IUiSpriteProvider sprites)
        {
            CancelIconLoad();
            if (sprites == null) return;

            _iconCts = new CancellationTokenSource();
            LoadIconsAsync(locationId, sprites, _iconCts.Token).Forget();
        }

        private async UniTaskVoid LoadIconsAsync(string locationId, IUiSpriteProvider sprites, CancellationToken ct)
        {
            // Snapshot active chips: the pool may be reused while we await.
            var items = _conditionsPool.ActiveElements().ToList();
            try
            {
                if (_locationImage != null && !string.IsNullOrEmpty(locationId))
                {
                    var locationSprite = await sprites.GetSpriteAsync(locationId, ct);
                    if (ct.IsCancellationRequested) return;
                    if (_locationImage != null) _locationImage.sprite = locationSprite;
                }

                foreach (var item in items)
                {
                    if (item == null) continue;
                    var sprite = await sprites.GetSpriteAsync(item.Genre, ct);
                    if (ct.IsCancellationRequested) return;
                    if (item != null) item.SetIcon(sprite);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void Cleanup()
        {
            _onStart = null;
            _locationId = null;
            CancelIconLoad();
            if (_locationImage != null) _locationImage.sprite = null;
            _conditionsPool.DisableAll();
        }

        private void CancelIconLoad()
        {
            if (_iconCts == null) return;
            _iconCts.Cancel();
            _iconCts.Dispose();
            _iconCts = null;
        }

        private void OnDestroy() => CancelIconLoad();
    }
}
