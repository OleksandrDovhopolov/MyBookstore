using Game.Bootstrap.Loading;
using UnityEngine;
using VContainer;

namespace Game.Bootstrap
{
    // Связывает scene-корень хаба с глобальным IGameFlowService, чтобы тот мог гасить/включать хаб
    // при входе/выходе из локации. Положить в GameplayScene на стабильный (не выключаемый) объект
    // и назначить _hubRoot = корневой GameObject визуала/UI хаба (НЕ глобальные сервисы, НЕ
    // DontDestroyOnLoad-канвас UIManager).
    //
    // Регистрируется через GameplayInstaller (RegisterComponentInHierarchy<HubRootBinder>()),
    // IGameFlowService резолвится из родителя (GlobalLifetimeScope).
    public sealed class HubRootBinder : MonoBehaviour
    {
        [Tooltip("Корневой GameObject хаба, который выключается на время локации.")]
        [SerializeField] private GameObject _hubRoot;

        private IGameFlowService _gameFlow;

        [Inject]
        public void Construct(IGameFlowService gameFlow)
        {
            _gameFlow = gameFlow;
        }

        private void Start()
        {
            if (_gameFlow == null)
            {
                Debug.LogWarning("[HubRootBinder] IGameFlowService was not injected — hub root not registered.");
                return;
            }

            if (_hubRoot == null)
            {
                Debug.LogWarning("[HubRootBinder] _hubRoot is not assigned — assign the hub visual root.");
                return;
            }

            _gameFlow.RegisterHubRoot(_hubRoot);
        }
    }
}
