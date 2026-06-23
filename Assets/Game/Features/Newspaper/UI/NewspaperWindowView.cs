using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Shop.API;
using Game.UI;
using Infrastructure;
using UIShared;
using UnityEngine;

namespace Game.Newspaper.UI
{
    public sealed class NewspaperWindowView : WindowView
    {
        [Header("Offer prefabs")]
        [SerializeField] private UIListPool<NewspaperOfferCardView> _bookCardsPool = new();
        [SerializeField] private UIListPool<NewspaperOfferCardView> _decorCardsPool = new();

        [Header("Temporary sprite addresses")]
        [SerializeField] private string _bookOfferSpriteAddress;
        [SerializeField] private string _vintageGlobeSpriteAddress;
        [SerializeField] private string _coffeePotSpriteAddress;

        // TODO temporary sprite loading; move to a dedicated service later.
        private Sprite _bookOfferSprite;
        private Sprite _vintageGlobeSprite;
        private Sprite _coffeePotSprite;
        private bool _spritesLoaded;

        public UIListPool<NewspaperOfferCardView> BookCardsPool => _bookCardsPool;
        public UIListPool<NewspaperOfferCardView> DecorCardsPool => _decorCardsPool;

        public async UniTask PreloadSpritesAsync(CancellationToken ct)
        {
            if (_spritesLoaded) return;

            ReleaseLoadedSprites();

            try
            {
                _bookOfferSprite = await LoadSpriteSafeAsync(_bookOfferSpriteAddress, ct);
                _vintageGlobeSprite = await LoadSpriteSafeAsync(_vintageGlobeSpriteAddress, ct);
                _coffeePotSprite = await LoadSpriteSafeAsync(_coffeePotSpriteAddress, ct);
                _spritesLoaded = true;
            }
            catch (OperationCanceledException)
            {
                ReleaseLoadedSprites();
                throw;
            }
        }

        public Sprite GetBookOfferIcon() => _bookOfferSprite;

        public Sprite GetDecorOfferIcon(string lotId)
        {
            if (string.Equals(lotId, NewspaperShopLotIds.DecorFreeVintageGlobe, StringComparison.Ordinal))
                return _vintageGlobeSprite;

            if (string.Equals(lotId, NewspaperShopLotIds.DecorPaidCoffeePot, StringComparison.Ordinal))
                return _coffeePotSprite;

            return null;
        }

        public void ReleaseLoadedSprites()
        {
            ReleaseSprite(ref _bookOfferSprite);
            ReleaseSprite(ref _vintageGlobeSprite);
            ReleaseSprite(ref _coffeePotSprite);
            _spritesLoaded = false;
        }

        private static async UniTask<Sprite> LoadSpriteSafeAsync(string address, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(address))
                return null;

            try
            {
                return await ProdAddressablesWrapper.LoadAsync<Sprite>(address, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NewspaperWindowView] Failed to load sprite '{address}': {ex.Message}");
                return null;
            }
        }

        private static void ReleaseSprite(ref Sprite sprite)
        {
            if (sprite == null) return;

            ProdAddressablesWrapper.Release(sprite);
            sprite = null;
        }
    }
}
