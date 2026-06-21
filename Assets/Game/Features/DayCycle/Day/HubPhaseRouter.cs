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
    // Слушает IDayProgressService.PhaseChanged. Morning root виден ТОЛЬКО в фазе Morning; в остальных
    // фазах скрыт. На Morning дополнительно закрывает ResultsWindow.
    // Preparation — это окно (PreparationWindow), закрывается само на Confirm; роутер им не управляет.
    // Sales — хаб целиком выключен GameFlowService, роутер ничего не делает.
    public sealed class HubPhaseRouter : MonoBehaviour
    {
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
            if (phase == DayPhase.Morning)
                HideResults();
        }

        private void HideResults()
        {
            if (_uiManager != null && _uiManager.IsWindowShown<ResultsWindow>())
                _uiManager.HideAsync<ResultsWindow>().Forget();
        }
    }
}
