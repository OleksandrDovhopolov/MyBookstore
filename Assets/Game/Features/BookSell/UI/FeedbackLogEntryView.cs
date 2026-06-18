using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Book.Sell.UI
{
    /// <summary>
    /// One line in the day's feedback log. <see cref="SalesScreenView"/> instantiates one
    /// of these per resolved request, per passive sale, and per book reservation, into a
    /// VerticalLayoutGroup container. The kind drives the visual treatment (label color +
    /// optional background).
    /// </summary>
    public sealed class FeedbackLogEntryView : MonoBehaviour
    {
        /// <summary>
        /// Categories of log entries. Order is significant — used as an index into
        /// <see cref="_labelColors"/> / <see cref="_backgroundColors"/>.
        /// </summary>
        public enum EntryKind
        {
            ActiveExcellent = 0,
            ActiveNormal = 1,
            ActiveFailed = 2,
            ActiveSkipped = 3,
            PassiveSale = 4,
            BookReserved = 5
        }

        [Header("Content")]
        [SerializeField] private TMP_Text _label;

        [Header("Optional visuals")]
        [Tooltip("Optional background image — left null if you don't want per-kind coloring.")]
        [SerializeField] private Image _background;

        [Tooltip("Text color per EntryKind. Array index matches the enum value (0..5).")]
        [SerializeField] private Color[] _labelColors = new Color[6]
        {
            Color.white,                      // ActiveExcellent
            Color.white,                      // ActiveNormal
            Color.white,                      // ActiveFailed
            Color.white,                      // ActiveSkipped
            Color.white,                      // PassiveSale
            new Color(0.4f, 0.7f, 1f)         // BookReserved — cyan-info, distinct from sales
        };

        [Tooltip("Background color per EntryKind. Used only if Background is assigned.")]
        [SerializeField] private Color[] _backgroundColors = new Color[6]
        {
            Color.clear, Color.clear, Color.clear, Color.clear, Color.clear, Color.clear
        };

        public void Bind(EntryKind kind, string text)
        {
            if (_label != null)
            {
                _label.text = text ?? string.Empty;

                var idx = (int)kind;
                if (idx >= 0 && idx < _labelColors.Length)
                    _label.color = _labelColors[idx];
            }

            if (_background != null)
            {
                var idx = (int)kind;
                if (idx >= 0 && idx < _backgroundColors.Length)
                    _background.color = _backgroundColors[idx];
            }
        }
    }
}
