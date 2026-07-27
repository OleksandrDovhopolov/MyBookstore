using Game.UI;
using UIShared;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Newspaper.UI
{
    public sealed class ShopWindowView : WindowView
    {
        [Header("Offer prefabs")]
        [FormerlySerializedAs("_bookCardsPool")]
        [SerializeField] private UIListPool<ShopItemView> _cardsPool = new();

        public UIListPool<ShopItemView> CardsPool => _cardsPool;
    }
}
