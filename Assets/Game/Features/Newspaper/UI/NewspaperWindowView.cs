using Game.UI;
using UIShared;
using UnityEngine;

namespace Game.Newspaper.UI
{
    public sealed class NewspaperWindowView : WindowView
    {
        [Header("Offer prefabs")]
        [SerializeField] private UIListPool<NewspaperOfferCardView> _bookCardsPool = new();
        [SerializeField] private UIListPool<NewspaperOfferCardView> _decorCardsPool = new();

        public UIListPool<NewspaperOfferCardView> BookCardsPool => _bookCardsPool;
        public UIListPool<NewspaperOfferCardView> DecorCardsPool => _decorCardsPool;
    }
}
