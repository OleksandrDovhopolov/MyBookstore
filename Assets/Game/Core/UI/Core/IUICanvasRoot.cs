using UnityEngine;

namespace Game.UI
{
    public interface IUICanvasRoot
    {
        Transform HudRoot { get; }
        Transform WindowsRoot { get; }
        GameObject Blocker { get; }
    }
}
