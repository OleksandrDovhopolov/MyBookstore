using Cysharp.Threading.Tasks;
using Game.UI;

namespace GameplayUI
{
    /// <summary>
    /// Opens HUD-owned windows while keeping the root gameplay HUD orchestration in the controller.
    /// </summary>
    public interface IHudWindowLauncher
    {
        UniTask OpenAsync<TWindow>(WindowArgs args = null)
            where TWindow : class, IWindowController, new();
    }
}
