using cheatModule;

namespace Game.Cheat
{
    public sealed class FpsCounterCheatModule : ICheatsModule
    {
        public void Initialize(ICheatsContainer cheatsContainer)
        {
            cheatsContainer.AddItem<CheatButtonItem>(item =>
                item.OnClick("Toggle FPS", FpsOverlay.ToggleVisible));
        }
    }
}
