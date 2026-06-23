using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Newspaper.UI
{
    public class RewardItemView : MonoBehaviour
    {
        [SerializeField] private Image _rewardImage;
        [SerializeField] private TextMeshProUGUI _rewardNameText;
        [SerializeField] private TextMeshProUGUI _rewardAmountText;
        [SerializeField] private RectTransform _animationStartPosition;
        
        private RewardSpecResource _rewardSpecResource;
        
        public void SetResourceData(RewardSpecResource rewardSpecResource)
        {
            _rewardSpecResource =  rewardSpecResource;
            SetReward(
                _rewardSpecResource.Icon,
                _rewardSpecResource.DisplayName ?? _rewardSpecResource.ResourceId,
                rewardSpecResource.Amount);
        }
        
        public void SetReward(Sprite sprite, string displayName, int amount)
        {
            if (_rewardImage != null)
                _rewardImage.sprite = sprite;
            if (_rewardNameText != null)
                _rewardNameText.text = displayName ?? string.Empty;
            if (_rewardAmountText != null)
                _rewardAmountText.text = Mathf.Max(0, amount).ToString();
        }
        
        public bool TryGetAnimationStartPosition(out Vector3 animationStartPosition)
        {
            if (_animationStartPosition != null)
            {
                animationStartPosition = _animationStartPosition.position;
                return true;
            }

            if (_rewardImage != null)
            {
                animationStartPosition = _rewardImage.rectTransform.position;
                return true;
            }

            animationStartPosition = transform.position;
            return false;
        }
        
        public void ResetView()
        {
            _rewardSpecResource = null;
            if (_rewardImage != null)
                _rewardImage.sprite = null;
            if (_rewardNameText != null)
                _rewardNameText.text = string.Empty;
            if (_rewardAmountText != null)
                _rewardAmountText.text = string.Empty;
        }
    }
}
