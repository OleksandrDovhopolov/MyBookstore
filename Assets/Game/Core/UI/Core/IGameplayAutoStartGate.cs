using System;

namespace Game.UI
{
    /// <summary>
    /// Temporarily gates gameplay auto-start flows while another first-run surface owns the screen.
    /// Explicit starts are not affected.
    /// </summary>
    public interface IGameplayAutoStartGate
    {
        bool IsBlocked { get; }

        event Action Released;

        void Block();
        void Release();
    }
}
