using UnityEngine;

namespace Game.UI
{
    public interface IWindow
    {
        GameObject GameObject { get; }
        RectTransform RectTransform { get; }
        CanvasGroup CanvasGroup { get; }
        Canvas Canvas { get; }
        WindowAnimation Animation { get; }
    }
}
