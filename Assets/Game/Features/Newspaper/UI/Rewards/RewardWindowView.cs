using System;
using System.Collections.Generic;
using Game.Rewards.API;
using Game.UI;
using UIShared;
using UnityEngine;

namespace Game.Newspaper.UI
{
    [Serializable]
    public class RewardSpecResource
    {
        public string ResourceId;
        public string DisplayName;
        public RewardKind Kind;
        public int Amount;
        public string Category;
        public Sprite Icon;
    }
    
    public class RewardWindowView : WindowView
    {
        [SerializeField] private UIListPool<RewardItemView> _cardGroupsPool;

        private readonly Dictionary<RewardSpecResource, RewardItemView> _rewardItemViews = new();
        
        public void SetReward(IReadOnlyList<RewardSpecResource> rewardSpecResources)
        {
            ResetView();
            if (rewardSpecResources == null || _cardGroupsPool == null) return;
            
            for (var i = 0; i < rewardSpecResources.Count; i++)
            {
                var rewardSpecResource = rewardSpecResources[i];
                if (rewardSpecResource == null) continue;

                var rewardItemView = _cardGroupsPool.GetNext();
                rewardItemView.SetResourceData(rewardSpecResource);
                _rewardItemViews.Add(rewardSpecResource, rewardItemView);
            }
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
        }
    }
}
