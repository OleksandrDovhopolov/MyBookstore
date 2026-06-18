using TMPro;
using UnityEngine;

namespace Game.WorldHud.SmokeTest
{
    [WorldHud("WorldHud/SmokeWorldHudBubble")]
    public sealed class SmokeWorldHudBubble : WorldHud
    {
        [SerializeField] private TextMeshProUGUI _label;

        public void SetText(string text)
        {
            if (_label != null) _label.text = text;
        }

        protected override void OnAttached()
        {
            if (CanvasGroup != null) CanvasGroup.alpha = 1f;
            if (_label != null && string.IsNullOrEmpty(_label.text)) _label.text = "Hello world";
        }
    }
}
