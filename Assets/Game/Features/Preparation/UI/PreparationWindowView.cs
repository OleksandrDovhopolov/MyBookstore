using Game.UI;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Preparation.UI
{
    public sealed class PreparationWindowView : WindowView
    {
        [Header("Texts")]
        [SerializeField] private TMP_Text _locationLabel;
        [SerializeField] private TMP_Text _slotCountLabel;

        [Header("Genre list")]
        [SerializeField] private UIListPool<PreparationGenreRowView> _genreRowPool = new();

        [Header("Actions")]
        [SerializeField] private Button _openShopButton;

        public Button OpenShopButton => _openShopButton;
        public UIListPool<PreparationGenreRowView> GenreRowPool => _genreRowPool;

        public void SetLocation(string value) => Set(_locationLabel, value);
        public void SetSlotCount(string value) => Set(_slotCountLabel, value);

        private static void Set(TMP_Text label, string value)
        {
            if (label != null) label.text = value ?? string.Empty;
        }
    }
}
