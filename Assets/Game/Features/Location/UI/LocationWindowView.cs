using Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Location.UI
{
    public sealed class LocationWindowView : WindowView
    {
        [Header("List")]
        [SerializeField] private Transform _listRoot;
        [SerializeField] private LocationRowView _rowTemplate;

        [Header("Footer")]
        [SerializeField] private Button _closeButton;

        public Transform ListRoot => _listRoot;
        public LocationRowView RowTemplate => _rowTemplate;
        public Button CloseButton => _closeButton;
    }
}
