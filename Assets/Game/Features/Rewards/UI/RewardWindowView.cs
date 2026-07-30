using System.Collections.Generic;
using Game.Inventory.API;
using Game.UI;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Rewards.UI
{
    //TODO move to Assets/Game/Features/Rewards ? 
    public class RewardWindowView : WindowView
    {
        [SerializeField] private GameObject _booksRoot;
        [SerializeField] private GameObject _decorRoot;
        [SerializeField] private UIListPool<RewardItemView> _cardGroupsPool;
        [SerializeField] private Image _decorImage;

        private readonly Dictionary<RewardSpecResource, RewardItemView> _rewardItemViews = new();
        private RewardSpecResource _decorRewardResource;
        
        public void SetReward(IReadOnlyList<RewardSpecResource> rewardSpecResources)
        {
            ResetView();
            if (rewardSpecResources == null) return;

            var decorReward = GetSingleDecorReward(rewardSpecResources);
            if (decorReward != null)
            {
                SetDecorReward(decorReward);
                return;
            }

            SetCardRewards(rewardSpecResources);
        }

        public RewardSpecResource GetDecorReward()
        {
            return _decorRewardResource;
        }

        public void SetDecorIcon(Sprite sprite)
        {
            if (_decorRewardResource == null || _decorImage == null) return;

            _decorImage.sprite = sprite;
        }

        private void SetCardRewards(IReadOnlyList<RewardSpecResource> rewardSpecResources)
        {
            SetMode(isDecor: false);
            if (_cardGroupsPool == null) return;
            
            for (var i = 0; i < rewardSpecResources.Count; i++)
            {
                var rewardSpecResource = rewardSpecResources[i];
                if (rewardSpecResource == null) continue;

                var rewardItemView = _cardGroupsPool.GetNext();
                rewardItemView.SetResourceData(rewardSpecResource);
                _rewardItemViews.Add(rewardSpecResource, rewardItemView);
            }
        }

        private void SetDecorReward(RewardSpecResource decorReward)
        {
            _decorRewardResource = decorReward;
            SetMode(isDecor: true);
            if (_decorImage == null) return;

            _decorImage.sprite = decorReward.Icon;
        }
        
        public Dictionary<RewardSpecResource, RewardItemView> GetViews()
        {
            return _rewardItemViews;
        }

        public void ResetView()
        {
            foreach (var rewardItemView in _rewardItemViews.Values)
            {
                rewardItemView.ResetView();
            }
            _rewardItemViews.Clear();
            _cardGroupsPool?.DisableAll();
            _decorRewardResource = null;
            SetMode(isDecor: false);
            if (_decorImage != null)
            {
                _decorImage.sprite = null;
            }
        }

        private void SetMode(bool isDecor)
        {
            SetActive(_booksRoot, !isDecor);
            SetActive(_decorRoot, isDecor);
        }

        private static RewardSpecResource GetSingleDecorReward(IReadOnlyList<RewardSpecResource> rewardSpecResources)
        {
            if (rewardSpecResources == null || rewardSpecResources.Count == 0) return null;
            RewardSpecResource decor = null;
            for (var i = 0; i < rewardSpecResources.Count; i++)
            {
                var rewardSpecResource = rewardSpecResources[i];
                if (!IsDecorReward(rewardSpecResource)) return null;
                decor = rewardSpecResource;
            }

            return decor;
        }

        private static bool IsDecorReward(RewardSpecResource rewardSpecResource) =>
            IsCategory(rewardSpecResource, InventoryCategories.Decor);

        private static bool IsCategory(RewardSpecResource rewardSpecResource, string category) =>
            rewardSpecResource != null
            && string.Equals(rewardSpecResource.Category, category, System.StringComparison.OrdinalIgnoreCase);

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }
    }
}
