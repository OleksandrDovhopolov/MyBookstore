using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using SpriteService;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Quest.UI
{
    public sealed class QuestRowView : MonoBehaviour, ICleanup
    {
        [SerializeField] private GameObject _portraitRoot;
        [SerializeField] private Image _portraitImage;
        [SerializeField] private Sprite _spriteFallback;
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private GameObject _completeBadge;
        [SerializeField] private UIListPool<QuestTaskRowView> _taskPool = new();
        [SerializeField] private GameObject _rewardRoot;
        [SerializeField] private UIListPool<QuestRewardIconView> _rewardPool = new();
        [SerializeField] private Button _claimButton;
        [SerializeField] private GameObject _claimedRoot;

        private Action<string> _onClaim;
        private string _questId;
        private CancellationTokenSource _spriteCts;

        private void Awake()
        {
            if (_claimButton != null)
                _claimButton.onClick.AddListener(OnClaimClicked);
        }

        public void Bind(QuestItemModel model, Action<string> onClaim, IUiSpriteProvider sprites)
        {
            _onClaim = onClaim;
            _questId = model?.Id;

            if (_completeBadge != null) _completeBadge.SetActive(model?.IsRewardClaimed == true);

            var hasPortrait = !string.IsNullOrEmpty(model?.CharacterId);
            if (_portraitRoot != null) _portraitRoot.SetActive(hasPortrait);
            if (_portraitImage != null) _portraitImage.sprite = _spriteFallback;

            var hasRewards = model?.IsRewardClaimed != true && model?.Rewards != null && model.Rewards.Count > 0;
            if (_rewardRoot != null) _rewardRoot.SetActive(hasRewards);
            if (_claimButton != null) _claimButton.gameObject.SetActive(model?.CanClaim == true);
            var claimedRoot = GetClaimedRoot();
            if (claimedRoot != null) claimedRoot.SetActive(model?.IsRewardClaimed == true);

            RenderTasks(model?.Tasks);
            RenderRewards(hasRewards ? model?.Rewards : null);
            LoadSprites(model, sprites);
        }

        public void Cleanup()
        {
            _onClaim = null;
            _questId = null;
            CancelSpriteLoad();
            if (_portraitImage != null) _portraitImage.sprite = _spriteFallback;
            if (_portraitRoot != null) _portraitRoot.SetActive(false);
            if (_titleLabel != null) _titleLabel.text = string.Empty;
            if (_completeBadge != null) _completeBadge.SetActive(false);
            if (_rewardRoot != null) _rewardRoot.SetActive(false);
            if (_claimButton != null) _claimButton.gameObject.SetActive(false);
            var claimedRoot = GetClaimedRoot();
            if (claimedRoot != null) claimedRoot.SetActive(false);
            _taskPool.DisableAll();
            _rewardPool.DisableAll();
        }

        private GameObject GetClaimedRoot()
            => _claimedRoot != null && (_claimButton == null || _claimedRoot != _claimButton.gameObject)
                ? _claimedRoot
                : null;

        private void RenderTasks(System.Collections.Generic.IReadOnlyList<QuestTaskItemModel> tasks)
        {
            if (!CanUsePool(_taskPool))
            {
                return;
            }

            _taskPool.DisableAll();
            if (tasks != null)
            {
                for (var i = 0; i < tasks.Count; i++)
                    _taskPool.GetNext().Bind(tasks[i]);
            }
            _taskPool.DisableNonActive();
        }

        private void RenderRewards(System.Collections.Generic.IReadOnlyList<QuestRewardItemModel> rewards)
        {
            if (!CanUsePool(_rewardPool)) return;

            _rewardPool.DisableAll();
            if (rewards != null)
            {
                for (var i = 0; i < rewards.Count; i++)
                    _rewardPool.GetNext().Bind(rewards[i]);
            }
            _rewardPool.DisableNonActive();
        }

        private void LoadSprites(QuestItemModel model, IUiSpriteProvider sprites)
        {
            CancelSpriteLoad();
            if (sprites == null || model == null) return;

            _spriteCts = new CancellationTokenSource();
            LoadSpritesAsync(model.CharacterPortraitKey, sprites, _spriteCts.Token).Forget();
        }

        private static bool CanUsePool<T>(UIListPool<T> pool) where T : MonoBehaviour
            => pool != null && pool.Prefab != null && pool.Parent != null;

        private async UniTaskVoid LoadSpritesAsync(string portraitKey, IUiSpriteProvider sprites, CancellationToken ct)
        {
            var rewardViews = CanUsePool(_rewardPool) ? _rewardPool.ActiveElements().ToList() : null;
            try
            {
                if (_portraitImage != null && !string.IsNullOrEmpty(portraitKey))
                {
                    var portrait = await sprites.GetSpriteAsync(portraitKey, ct);
                    if (ct.IsCancellationRequested) return;
                    if (_portraitImage != null) _portraitImage.sprite = portrait != null ? portrait : _spriteFallback;
                }

                if (rewardViews == null) return;

                for (var i = 0; i < rewardViews.Count; i++)
                {
                    var rewardView = rewardViews[i];
                    if (rewardView == null || string.IsNullOrEmpty(rewardView.SpriteId)) continue;

                    var sprite = await sprites.GetSpriteAsync(rewardView.SpriteId, ct);
                    if (ct.IsCancellationRequested) return;
                    if (rewardView != null) rewardView.SetIcon(sprite);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void OnClaimClicked()
        {
            if (!string.IsNullOrEmpty(_questId))
                _onClaim?.Invoke(_questId);
        }

        private void CancelSpriteLoad()
        {
            if (_spriteCts == null) return;
            _spriteCts.Cancel();
            _spriteCts.Dispose();
            _spriteCts = null;
        }

        private void OnDestroy() => CancelSpriteLoad();
    }
}
