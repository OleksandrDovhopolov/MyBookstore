using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI.ContentWidget;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Decor.UI
{
    public sealed class DecorInfoWidgetView : MonoBehaviour, IContentWidgetView
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private TextMeshProUGUI _descriptionLabel;
        [SerializeField] private UIListPool<DecorBonusItemView> _bonusesPool = new();
        [SerializeField] private UIListPool<DecorCharacteristicItemView> _characteristicsPool = new();
        [SerializeField] private Sprite _characteristicIconPlaceholder;

        public bool Setup(ContentWidgetDataBase data)
        {
            if (data is not DecorInfoWidgetData decorInfo)
                return false;

            if (_icon != null)
                _icon.sprite = decorInfo.Icon;

            if (_nameLabel != null)
                _nameLabel.text = decorInfo.Name ?? string.Empty;

            if (_descriptionLabel != null)
                _descriptionLabel.text = decorInfo.Description ?? string.Empty;

            //RenderBonuses(decorInfo);
            //RenderCharacteristics(decorInfo);
            return true;
        }

        public async UniTask OnViewCreatedAsync(CancellationToken ct)
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, ct);

            if (transform is RectTransform rect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                Canvas.ForceUpdateCanvases();
            }
        }

        private void RenderBonuses(DecorInfoWidgetData data)
        {
            if (_bonusesPool == null) return;

            _bonusesPool.DisableAll();
            foreach (var bonus in data.Bonuses)
            {
                if (bonus == null) continue;
                _bonusesPool.GetNext().Bind(
                    bonus.GenreSprite,
                    bonus.Genre,
                    bonus.PercentText,
                    bonus.PercentColor);
            }

            _bonusesPool.DisableNonActive();
        }

        private void RenderCharacteristics(DecorInfoWidgetData data)
        {
            if (_characteristicsPool == null) return;

            _characteristicsPool.DisableAll();
            foreach (var characteristic in data.Characteristics)
            {
                if (string.IsNullOrEmpty(characteristic)) continue;
                _characteristicsPool.GetNext().Bind(_characteristicIconPlaceholder, characteristic);
            }

            _characteristicsPool.DisableNonActive();
        }
    }
}
