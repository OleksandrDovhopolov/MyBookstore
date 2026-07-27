using Game.UI;
using UIShared;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Newspaper.UI
{
    public sealed class NewspaperWindowView : WindowView
    {
        [Header("Offer prefabs")]
        [FormerlySerializedAs("_bookCardsPool")]
        [SerializeField] private UIListPool<NewspaperOfferCardView> _cardsPool = new();

        public UIListPool<NewspaperOfferCardView> CardsPool => _cardsPool;
    }
}
