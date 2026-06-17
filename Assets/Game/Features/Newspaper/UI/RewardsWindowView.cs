using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Newspaper.UI
{
    public sealed class RewardsWindowView : WindowView
    {
        [Header("Layout")]
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _bodyLabel;
        [SerializeField] private Button _okButton;

        public TMP_Text TitleLabel => _titleLabel;
        public TMP_Text BodyLabel => _bodyLabel;
        public Button OkButton => _okButton;
    }
}
