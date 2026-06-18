using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Shop.API;
using Game.UI;
using VContainer;

namespace Game.Shop.UI
{
    /// <summary>
    /// Permanent shop window with three tabs (Books / Boxes / Decor), opened from the HUD button.
    /// Phase 1 MVP: stub ассортимент per tab, no daily reset, no per-tab visual state — just three
    /// sections wired to <c>classic.books</c> / <c>classic.boxes</c> / <c>classic.decor</c> storefronts.
    /// </summary>
    [Window("ClassicShopWindow", WindowType.Page)]
    public sealed class ClassicShopWindow : WindowController<ClassicShopWindowView>
    {
        private IShopService _shop;
        private IShopConfirmationPolicy _confirmPolicy;
        private CancellationTokenSource _cts;
        private string _activeStorefront = ShopStorefrontIds.ClassicBooks;

        [Inject]
        public void InjectServices(IShopService shop, IShopConfirmationPolicy confirmPolicy)
        {
            _shop = shop;
            _confirmPolicy = confirmPolicy;
        }

        protected override void OnInit()
        {
            _cts = new CancellationTokenSource();

            if (View.BooksTabButton != null)
                View.BooksTabButton.onClick.AddListener(OnBooksTabClicked);
            if (View.BoxesTabButton != null)
                View.BoxesTabButton.onClick.AddListener(OnBoxesTabClicked);
            if (View.DecorTabButton != null)
                View.DecorTabButton.onClick.AddListener(OnDecorTabClicked);
            if (View.CloseButton != null)
                View.CloseButton.onClick.AddListener(OnCloseClicked);

            // Bind each section to its storefront. Sections own their internal purchase flow
            // (confirmation + RewardsWindow popup) — controller only triggers tab swap + refresh.
            if (View.BooksSection != null)
                View.BooksSection.Bind(ShopStorefrontIds.ClassicBooks, _shop, _confirmPolicy, UIManager, _cts.Token);
            if (View.BoxesSection != null)
                View.BoxesSection.Bind(ShopStorefrontIds.ClassicBoxes, _shop, _confirmPolicy, UIManager, _cts.Token);
            if (View.DecorSection != null)
                View.DecorSection.Bind(ShopStorefrontIds.ClassicDecor, _shop, _confirmPolicy, UIManager, _cts.Token);
        }

        protected override void OnShowStart() => SelectTab(ShopStorefrontIds.ClassicBooks);

        protected override void OnDispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (View == null) return;
            if (View.BooksTabButton != null) View.BooksTabButton.onClick.RemoveListener(OnBooksTabClicked);
            if (View.BoxesTabButton != null) View.BoxesTabButton.onClick.RemoveListener(OnBoxesTabClicked);
            if (View.DecorTabButton != null) View.DecorTabButton.onClick.RemoveListener(OnDecorTabClicked);
            if (View.CloseButton != null) View.CloseButton.onClick.RemoveListener(OnCloseClicked);
        }

        private void OnBooksTabClicked() => SelectTab(ShopStorefrontIds.ClassicBooks);
        private void OnBoxesTabClicked() => SelectTab(ShopStorefrontIds.ClassicBoxes);
        private void OnDecorTabClicked() => SelectTab(ShopStorefrontIds.ClassicDecor);

        private void SelectTab(string storefrontId)
        {
            _activeStorefront = storefrontId;

            if (View.BooksSection != null) View.BooksSection.gameObject.SetActive(storefrontId == ShopStorefrontIds.ClassicBooks);
            if (View.BoxesSection != null) View.BoxesSection.gameObject.SetActive(storefrontId == ShopStorefrontIds.ClassicBoxes);
            if (View.DecorSection != null) View.DecorSection.gameObject.SetActive(storefrontId == ShopStorefrontIds.ClassicDecor);

            if (View.ActiveTabLabel != null)
                View.ActiveTabLabel.text = LabelFor(storefrontId);

            // Refresh the active section in case lots changed (purchase elsewhere, day rollover, etc).
            switch (storefrontId)
            {
                case ShopStorefrontIds.ClassicBooks: View.BooksSection?.Refresh(); break;
                case ShopStorefrontIds.ClassicBoxes: View.BoxesSection?.Refresh(); break;
                case ShopStorefrontIds.ClassicDecor: View.DecorSection?.Refresh(); break;
            }
        }

        private static string LabelFor(string storefrontId) => storefrontId switch
        {
            ShopStorefrontIds.ClassicBooks => "Books",
            ShopStorefrontIds.ClassicBoxes => "Crates",
            ShopStorefrontIds.ClassicDecor => "Decor",
            _ => storefrontId,
        };

        private void OnCloseClicked() => CloseAsync().Forget();
    }
}
