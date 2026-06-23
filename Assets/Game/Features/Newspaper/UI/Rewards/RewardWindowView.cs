using System;
using System.Collections.Generic;
using Game.UI;
using UIShared;
using UnityEngine;

namespace Game.Newspaper
{
    public enum RewardKind
    {
        Unknown = 0,
        Resource = 1,
        InventoryItem = 2,
    }
    
    [Serializable]
    public class RewardSpecResource
    {
        public string ResourceId;
        public RewardKind Kind = RewardKind.Unknown;
        public int Amount;
        public string Category;
        public Sprite Icon;
    }
    
    public class RewardWindowView : WindowView
    {
        [SerializeField] private UIListPool<RewardItemView> _cardGroupsPool;

        private readonly Dictionary<RewardSpecResource, RewardItemView> _rewardItemViews = new();
        
        public void SetReward(List<RewardSpecResource> rewardSpecResources)
        {
            _rewardItemViews.Clear();
            _cardGroupsPool.DisableNonActive();
            
            foreach (var rewardSpecResource in rewardSpecResources)
            {
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
            _cardGroupsPool.DisableAll();
        }
    }
}
