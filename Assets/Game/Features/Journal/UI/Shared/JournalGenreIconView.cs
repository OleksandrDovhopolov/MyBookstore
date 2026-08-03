using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Journal.UI
{
    public sealed class JournalGenreIconView : MonoBehaviour, ICleanup
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _nameLabel;

        public string Genre { get; private set; }

        public void Bind(string genre, Sprite icon)
        {
            Genre = genre;
            if (_nameLabel != null) _nameLabel.text = genre ?? string.Empty;
            SetIcon(icon);
        }

        public void SetIcon(Sprite icon)
        {
            if (_icon == null) return;
            _icon.sprite = icon;
            _icon.gameObject.SetActive(icon != null);
        }

        public void Cleanup()
        {
            Genre = null;
            if (_nameLabel != null) _nameLabel.text = string.Empty;
            SetIcon(null);
        }
    }
}
