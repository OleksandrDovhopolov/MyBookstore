using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.LocationUnlock.API;
using SpriteService;
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
        [SerializeField] private UIListPool<LocationConditionItemView> _costsPool = new();
        [SerializeField] private TextMeshProUGUI _entryCostLabel;
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _unlockButton;
        [SerializeField] private Button _demandInfoButton;

        private Action<string> _onStart;
        private Action<string> _onUnlock;
        private Action<string, RectTransform> _onDemandInfo;
        private string _locationId;
        private CancellationTokenSource _iconCts;

        private void Awake()
        {
            if (_startButton != null)
                _startButton.onClick.AddListener(() => _onStart?.Invoke(_locationId));
            if (_unlockButton != null)
                _unlockButton.onClick.AddListener(() => _onUnlock?.Invoke(_locationId));
            if (_demandInfoButton != null)
            {
                _demandInfoButton.onClick.AddListener(() =>
                    _onDemandInfo?.Invoke(_locationId, _demandInfoButton.transform as RectTransform));
            }
        }

        public void Bind(
            LocationListItemModel model,
            Action<string> onStart,
            Action<string> onUnlock,
            Action<string, RectTransform> onDemandInfo,
            IUiSpriteProvider sprites)
        {
            _onStart = onStart;
            _onUnlock = onUnlock;
            _onDemandInfo = onDemandInfo;
            _locationId = model.LocationId;

            if (_nameLabel != null) _nameLabel.text = model.DisplayName;
            if (_entryCostLabel != null) _entryCostLabel.text = $"{model.EntryCost} {model.EntryCurrencyId}";

            if (_startButton != null)
            {
                _startButton.gameObject.SetActive(model.IsUnlocked);
                _startButton.interactable = model.StartEnabled;
            }
            if (_unlockButton != null)
            {
                _unlockButton.gameObject.SetActive(!model.IsUnlocked && model.Costs.Count > 0);
                _unlockButton.interactable = model.CanUnlock;
            }
            if (_lockedPanel != null) _lockedPanel.SetActive(!model.IsUnlocked);

            RenderConditions(model.Conditions);
            RenderCosts(model.Costs);
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

        private void RenderCosts(IReadOnlyList<LocationUnlockCostProgress> costs)
        {
            _costsPool.DisableAll();

            if (costs != null)
            {
                for (var i = 0; i < costs.Count; i++)
                    _costsPool.GetNext().Bind(costs[i]);
            }

            _costsPool.DisableNonActive();
        }

        private void LoadIcons(string locationId, IUiSpriteProvider sprites)
        {
            CancelIconLoad();
            if (sprites == null) return;

            _iconCts = new CancellationTokenSource();
            LoadIconsAsync(locationId, sprites, _iconCts.Token).Forget();
        }

        private async UniTaskVoid LoadIconsAsync(string locationId, IUiSpriteProvider sprites, CancellationToken ct)
        {
            var items = _conditionsPool.ActiveElements()
                .Concat(_costsPool.ActiveElements())
                .ToList();
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
                    if (string.IsNullOrEmpty(item.SpriteId)) continue;
                    var sprite = await sprites.GetSpriteAsync(item.SpriteId, ct);
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
            _onUnlock = null;
            _onDemandInfo = null;
            _locationId = null;
            CancelIconLoad();
            if (_locationImage != null) _locationImage.sprite = null;
            _conditionsPool.DisableAll();
            _costsPool.DisableAll();
            if (_startButton != null) _startButton.gameObject.SetActive(false);
            if (_unlockButton != null) _unlockButton.gameObject.SetActive(false);
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
