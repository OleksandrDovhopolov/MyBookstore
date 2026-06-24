using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Preparation.UI
{
    /// <summary>
    /// One proportional segment of the shelf preview bar. Width is driven by the picked count via
    /// <see cref="LayoutElement.flexibleWidth"/>; tapping the segment removes one book of its genre.
    /// Pooled and rebound — never instantiated per update.
    /// </summary>
    public sealed class ShelfPreviewSegmentView : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private LayoutElement _layout;
        [SerializeField] private TMP_Text _countLabel;
        [SerializeField] private Button _button;

        private Action<string> _onClick;

        public string Genre { get; private set; }

        private void Awake()
        {
            if (_button != null) _button.onClick.AddListener(OnClicked);
        }

        public void Bind(string genre, Color color, int pickedCount, Action<string> onClick)
        {
            Genre = genre;
            _onClick = onClick;

            if (_background != null) _background.color = color;
            if (_countLabel != null) _countLabel.text = pickedCount.ToString();
            if (_layout != null) _layout.flexibleWidth = pickedCount;
        }

        private void OnClicked() => _onClick?.Invoke(Genre);

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(OnClicked);
        }
    }
}
