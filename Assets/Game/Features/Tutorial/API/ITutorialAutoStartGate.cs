using System;

namespace Game.Tutorial.API
{
    /// <summary>
    /// Temporarily gates tutorial auto-start triggers while another first-run surface owns the screen.
    /// Explicit tutorial starts are not affected.
    /// </summary>
    public interface ITutorialAutoStartGate
    {
        bool IsBlocked { get; }

        event Action Released;

        void Block();
        void Release();
    }
}
