using Game.Progression.API;
using Game.Resources.API;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.Resources.UI
{
    /// <summary>
    /// Debug HUD: shows current gold and reputation. Place it as an inactive (or active) GameObject
    /// in GameplayScene; the view is registered via <c>RegisterComponentInHierarchy</c> only when
    /// present in the hierarchy. Production HUD lands with the UI System module.
    /// </summary>
    public sealed class GameHudView : MonoBehaviour
    {
        [Header("Labels")]
        [SerializeField] private TMP_Text _goldLabel;
        [SerializeField] private TMP_Text _reputationLabel;

        private IResourcesService _resources;
        private IProgressionService _progression;

        [Inject]
        public void Construct(IResourcesService resources, IProgressionService progression)
        {
            _resources = resources;
            _progression = progression;
        }

        private void Start()
        {
            if (_resources == null || _progression == null)
            {
                Debug.LogWarning("[GameHudView] dependencies missing — not registered in DI?");
                return;
            }

            _resources.Changed += OnResourceChanged;
            _progression.Changed += OnProgressionChanged;
            Refresh();
        }

        private void Refresh()
        {
            if (_goldLabel != null)
                _goldLabel.text = $"Gold: {_resources.GetAmount(ResourceIds.Gold)}";
            if (_reputationLabel != null)
                _reputationLabel.text = $"Rep: {_progression.Reputation}";
        }

        private void OnResourceChanged(ResourceChangeEvent _) => Refresh();
        private void OnProgressionChanged(ProgressionChangeEvent _) => Refresh();

        private void OnDestroy()
        {
            if (_resources != null) _resources.Changed -= OnResourceChanged;
            if (_progression != null) _progression.Changed -= OnProgressionChanged;
        }
    }
}
