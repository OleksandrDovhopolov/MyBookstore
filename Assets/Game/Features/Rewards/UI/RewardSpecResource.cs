using System;
using Game.Rewards.API;
using UnityEngine;

namespace Game.Rewards.UI
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
}
