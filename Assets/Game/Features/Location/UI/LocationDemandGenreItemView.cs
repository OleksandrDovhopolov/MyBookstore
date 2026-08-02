using Game.Configs.Models;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Location.UI
{
    public sealed class LocationDemandGenreItemView : MonoBehaviour, ICleanup
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _nameLabel;

        public void Bind(BookGenre genre, Sprite icon)
        {
            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.gameObject.SetActive(icon != null);
            }

            if (_nameLabel != null)
                _nameLabel.text = genre.ToConfigValue();
        }

        public void Cleanup()
        {
            if (_icon != null)
            {
                _icon.sprite = null;
                _icon.gameObject.SetActive(false);
            }

            if (_nameLabel != null)
                _nameLabel.text = string.Empty;
        }
    }
}
