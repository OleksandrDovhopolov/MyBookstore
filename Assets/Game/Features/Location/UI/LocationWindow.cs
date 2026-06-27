using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.LocationUnlock.API;
using Game.UI;
using UnityEngine;
using VContainer;

namespace Game.Location.UI
{
    [Window("UI/Location/LocationWindow", WindowType.Page)]
    public sealed class LocationWindow : WindowController<LocationWindowView>, IResultWindow<string>
    {
        private IConfigsService _configs;
        private ILocationUnlockService _unlock;
        private readonly List<LocationRowView> _rows = new();

        public string Result { get; private set; }

        [Inject]
        public void InjectServices(IConfigsService configs, ILocationUnlockService unlock)
        {
            _configs = configs;
            _unlock = unlock;
        }

        protected override void OnInit()
        {
            if (View.RowTemplate != null) View.RowTemplate.gameObject.SetActive(false);
            if (View.CloseButton != null) View.CloseButton.onClick.AddListener(OnCloseClicked);
        }

        protected override void OnShowStart()
        {
            Result = null;

            if (_unlock != null)
            {
                _unlock.Unlocked += OnUnlockChanged;
                _unlock.StatusChanged += OnUnlockChanged;
            }

            Render();
        }

        protected override void OnHideStart(bool isClosed)
        {
            if (_unlock != null)
            {
                _unlock.Unlocked -= OnUnlockChanged;
                _unlock.StatusChanged -= OnUnlockChanged;
            }
        }

        protected override void OnDispose() => ClearRows();

        //TODO check is this possible that location status could be changed during LocationWindow opened
        private void OnUnlockChanged(string _) => Render();

        private void Render()
        {
            ClearRows();

            if (_configs == null || View.RowTemplate == null || View.ListRoot == null)
            {
                Debug.LogWarning("[LocationWindow] missing configs service or view template; nothing to render.");
                return;
            }

            foreach (var config in _configs.GetAll<LocationConfig>())
            {
                if (config == null || string.IsNullOrEmpty(config.Id)) continue;

                var status = _unlock?.GetStatus(config.Id);
                var model = LocationListItemModel.From(config, status);

                var row = Object.Instantiate(View.RowTemplate, View.ListRoot);
                var locationId = model.LocationId;
                row.Bind(model, () => OnStartClicked(locationId));
                row.gameObject.SetActive(true);
                _rows.Add(row);
            }
        }

        private void OnStartClicked(string locationId)
        {
            Result = locationId;
            CloseAsync().Forget();
        }

        private void OnCloseClicked() => CloseAsync().Forget();

        private void ClearRows()
        {
            for (var i = 0; i < _rows.Count; i++)
                if (_rows[i] != null) Object.Destroy(_rows[i].gameObject);
            _rows.Clear();
        }
    }
}
