using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Location.UI
{
    public sealed class LocationConditionItemView : MonoBehaviour, ICleanup
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _countLabel;

        private string _genre;
        public string Genre => _genre;

        public void Bind(LocationConditionProgress progress)
        {
            _genre = progress.Genre;
            if (_countLabel != null) _countLabel.text = $"{progress.Current}/{progress.Target}";
        }

        public void SetIcon(Sprite sprite)
        {
            if (_icon != null) _icon.sprite = sprite;
        }

        public void Cleanup()
        {
            _genre = null;
            if (_icon != null) _icon.sprite = null;
        }
    }
}
