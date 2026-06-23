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

        [Header("Genre icons")]
        [SerializeField] private Sprite _classicSprite;
        [SerializeField] private Sprite _crimeSprite;
        [SerializeField] private Sprite _dramaSprite;
        [SerializeField] private Sprite _factSprite;
        [SerializeField] private Sprite _fantasySprite;
        [SerializeField] private Sprite _kidsSprite;
        [SerializeField] private Sprite _travelSprite;

        [Header("Fallback icons")]
        [SerializeField] private Sprite _nonBookRewardSprite;

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

        public Sprite GetIconForReward(string resourceId)
        {
            if (string.Equals(resourceId, "Classic", StringComparison.OrdinalIgnoreCase)) return _classicSprite;
            if (string.Equals(resourceId, "Crime", StringComparison.OrdinalIgnoreCase)) return _crimeSprite;
            if (string.Equals(resourceId, "Drama", StringComparison.OrdinalIgnoreCase)) return _dramaSprite;
            if (string.Equals(resourceId, "Fact", StringComparison.OrdinalIgnoreCase)) return _factSprite;
            if (string.Equals(resourceId, "Fantasy", StringComparison.OrdinalIgnoreCase)) return _fantasySprite;
            if (string.Equals(resourceId, "Kids", StringComparison.OrdinalIgnoreCase)) return _kidsSprite;
            if (string.Equals(resourceId, "Travel", StringComparison.OrdinalIgnoreCase)) return _travelSprite;

            return _nonBookRewardSprite;
        }
    }
}
