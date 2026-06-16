using System;
using System.Collections.Generic;
using cheatModule;
using Cysharp.Threading.Tasks;
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

        [Inject]
        private void Construct(UIManager uiManager)
        {
            _uiManager = uiManager;
        }
        
        public void Start()
        {
            InitializeRootPanel();
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
                new DecorationCheatModule(_uiManager, destroyCt),
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
