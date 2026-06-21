using Cysharp.Threading.Tasks;
using Game.DayCycle.Results.UI;
using Game.UI;
using UnityEngine;
using VContainer;

namespace Game.DayCycle.Day
{
    // PhaseRouter-lite: отвечает ТОЛЬКО за визуальное состояние хаба при смене фазы дня.
    // Сценами не управляет (это GameFlowService) и про LocationScene не знает.
    //
    // Слушает IDayProgressService.PhaseChanged и:
    //   Morning  → показать Morning root, спрятать Preparation root, закрыть ResultsWindow;
    //   Results  → спрятать Morning и Preparation roots (под окном Results — чистый хаб).
    // Фазу Preparation выставляет GameplaySceneController (Morning → Preparation), здесь не дублируем.
    // Фаза Sales — хаб целиком выключен GameFlowService, роутер ничего не делает.
    //
    // Размещается в GameplayScene на стабильном объекте; регистрируется через хаб-installer
    // (RegisterComponentInHierarchy<HubPhaseRouter>()). _morningScreenRoot/_preparationScreenRoot —
    // те же корни, что и в GameplaySceneView.
    public sealed class HubPhaseRouter : MonoBehaviour
    {
        [SerializeField] private GameObject _morningScreenRoot;
        [SerializeField] private GameObject _preparationScreenRoot;

        private IDayProgressService _dayProgress;
        private IUIManager _uiManager;

        [Inject]
        public void Construct(IDayProgressService dayProgress, IUIManager uiManager)
        {
            _dayProgress = dayProgress;
            _uiManager = uiManager;
        }

        private void Start()
        {
            if (_dayProgress == null)
            {
                Debug.LogWarning("[HubPhaseRouter] IDayProgressService not injected — router inactive.");
                return;
            }

            _dayProgress.PhaseChanged += OnPhaseChanged;
            ApplyPhase(_dayProgress.Current.CurrentPhase);
        }

        private void OnDestroy()
        {
            if (_dayProgress != null)
                _dayProgress.PhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(DayProgressState state) => ApplyPhase(state.CurrentPhase);

        private void ApplyPhase(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Morning:
                    SetActive(_morningScreenRoot, true);
                    SetActive(_preparationScreenRoot, false);
                    HideResults();
                    break;

                case DayPhase.Results:
                    SetActive(_morningScreenRoot, false);
                    SetActive(_preparationScreenRoot, false);
                    break;

                // Preparation: переключает GameplaySceneController.
                // Sales: хаб выключен GameFlowService — здесь ничего не делаем.
            }
        }

        private void HideResults()
        {
            if (_uiManager != null && _uiManager.IsWindowShown<ResultsWindow>())
                _uiManager.HideAsync<ResultsWindow>().Forget();
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }
    }
}
