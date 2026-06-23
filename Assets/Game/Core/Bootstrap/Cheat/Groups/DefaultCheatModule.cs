using cheatModule;
using Cysharp.Threading.Tasks;
using Game.Decor.UI;
using Game.Inventory.UI;
using Game.Shop.UI;
using Game.UI;

namespace Game.Cheat
{
    public sealed class DefaultCheatModule : ICheatsModule
    {
        private readonly UIManager _uiManager;
        
        public DefaultCheatModule(UIManager uiManager)
        {
            _uiManager =  uiManager;
        }

        public void Initialize(ICheatsContainer cheatsContainer)
        {
            cheatsContainer.AddItem<CheatButtonItem>(item =>
                item.OnClick("Open Inventory", () =>
                {
                    _uiManager.ShowAsync<InventoryWindowController>().Forget();
                }));

            cheatsContainer.AddItem<CheatButtonItem>(item =>
                item.OnClick("Open Decoration", () =>
                {
                    _uiManager.ShowAsync<DecorPlacementWindow>().Forget();
                }));

            cheatsContainer.AddItem<CheatButtonItem>(item =>
                item.OnClick("Open Shop", () =>
                {
                    _uiManager.ShowAsync<ClassicShopWindow>().Forget();
                }));
        }
    }
}
