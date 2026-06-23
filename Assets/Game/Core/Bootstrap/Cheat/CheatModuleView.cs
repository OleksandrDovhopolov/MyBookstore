using System;
using System.Collections.Generic;
using cheatModule;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Inventory.API;
using Game.Resources.API;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.Cheat
{
    public class CheatModuleView : MonoBehaviour, ICheatsContainer
    {
        [SerializeField] private CheatsManager _cheatsManagerPrefab;
        [SerializeField] private Button _cheatButton;

        private CheatsManager _cheatsManager;
        private CheatPanelItem _rootPanel;
        private List<ICheatsModule> _cheatsModules;

        private UIManager _uiManager;
        private IInventoryService _inventory;
        private IConfigsService _configs;
        private IResourcesService _resources;

        // ISalesDayController is intentionally NOT injected here: this view lives in a UI window
        // prefab instantiated by the global UI factory, while the controller is registered in the
        // gameplay scope. A direct [Inject] fails at construction time and a TryResolve on the
        // global IObjectResolver returns false. SalesCheatModule looks the controller up via the
        // active SalesScreenView in the scene instead.

        [Inject]
        private void Construct(UIManager uiManager, IInventoryService inventory, IConfigsService configs, IResourcesService resources)
        {
            _uiManager = uiManager;
            _inventory = inventory;
            _configs = configs;
            _resources = resources;
        }

        public void Start()
        {
            InitializeRootPanel();
            // Cheat-модули читают конфиги синхронно (GetAll<DecorConfig> и т.п.). Если Start успел до
            // ConfigsService.WarmupAsync — список будет пустым + warning. Ждём прогрев (идемпотентный) и
            // только потом строим модули.
            InitializeCheatsModulesAsync().Forget();
        }

        private async UniTaskVoid InitializeCheatsModulesAsync()
        {
            if (_configs != null)
            {
                try
                {
                    await _configs.WarmupAsync(this.GetCancellationTokenOnDestroy());
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            InitializeCheatsModules();
        }

        private void OnEnable()
        {
            _cheatButton.onClick.AddListener(OpenCheatPanel);
        }

        private void OnDisable()
        {
            _cheatButton.onClick.RemoveListener(OpenCheatPanel);
        }

        private void InitializeRootPanel()
        {
            if (_cheatsManager == null)
            {
                _cheatsManager = Instantiate(_cheatsManagerPrefab);
            }

            if (_cheatsManager == null)
            {
                throw new NullReferenceException("_cheatsManager");
            }

            _rootPanel = _cheatsManager.GetCheatItem<CheatPanelItem>();
            _rootPanel.transform.SetParent(_cheatsManager.transform, false);
        }

        private void InitializeCheatsModules()
        {
            _cheatsModules = new List<ICheatsModule>(GetCheatModules());
            _cheatsModules.ForEach(module =>
            {
                module.Initialize(this);
            });
        }

        public void AddItem<T>(Action<T> initializer) where T : CheatItem
        {
            _rootPanel.AddItem(initializer);
        }

        protected virtual List<ICheatsModule> GetCheatModules()
        {
            var destroyCt = this.GetCancellationTokenOnDestroy();

            var cheatsModules = new List<ICheatsModule>
            {
                new DefaultCheatModule(_uiManager),
                new DecorationCheatModule(_uiManager, _inventory, _configs, destroyCt),
                new ResourcesCheatModule(_resources, destroyCt)
            };

            return cheatsModules;
        }

        public void OpenCheatPanel()
        {
            if (_cheatsManager == null)
            {
                Debug.LogWarning("Failed to open inventory window. Inventory services are not initialized.");
                throw new NullReferenceException("_cheatsManager");
            }

            _cheatsManager.ShowPanel(_rootPanel);
        }
    }
}
