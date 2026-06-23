using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Newspaper
{
    public class RewardItemView : MonoBehaviour
    {
        [SerializeField] private Image _rewardImage;
        [SerializeField] private TextMeshProUGUI _rewardAmountText;
        [SerializeField] private RectTransform _animationStartPosition;
        
        private RewardSpecResource _rewardSpecResource;
        
        public void SetResourceData(RewardSpecResource rewardSpecResource)
        {
            _rewardSpecResource =  rewardSpecResource;
            SetReward(_rewardSpecResource.Icon,  rewardSpecResource.Amount);
        }
        
        public void SetReward(Sprite sprite, int amount)
        {
            _rewardImage.sprite = sprite;
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
            _rewardImage.sprite = null;
            _rewardAmountText.text = string.Empty;
        }
    }
}
