using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.DayCycle.Morning.Model;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.DayCycle.Morning.UI
{
    //TODO remove this class 
    /// <summary>
    /// Debug-экран утра для вертикального среза core loop (uGUI). Не продакшен-окно:
    /// финальный UI приедет с модулем UI System. Вся логика — в IMorningSessionService;
    /// view только показывает контекст и дёргает «К подготовке».
    /// Размещается в GameplayScene; регистрируется через RegisterComponentInHierarchy.
    /// </summary>
    public sealed class MorningScreenView : MonoBehaviour
    {
        [Header("Texts")]
        [SerializeField] private TMP_Text _dayLabel;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _weatherLabel;
        [SerializeField] private TMP_Text _summaryLabel;
        [SerializeField] private TMP_Text _hintLabel;
        [SerializeField] private TMP_Text _modifierLabel;

        private IMorningSessionService _session;
        private readonly CancellationTokenSource _cts = new();
        private bool _started;

        [Inject]
        public void Construct(IMorningSessionService session)
        {
            _session = session;
        }

        private void Start()
        {
            if (_session == null)
            {
                Debug.LogWarning("[MorningScreenView] IMorningSessionService не внедрён — экран не зарегистрирован в DI?");
                return;
            }

            _started = true;
            RefreshAsync(_cts.Token).Forget();
        }

        private void OnEnable()
        {
            // Повторный вход в новый день: HubPhaseRouter снова включил Morning root.
            // Первый enable (до Start/инъекции) пропускаем — начальный refresh делает Start.
            if (_started && _session != null)
                RefreshAsync(_cts.Token).Forget();
        }

        private async UniTaskVoid RefreshAsync(CancellationToken ct)
        {
            var context = await _session.StartOrResumeAsync(ct);
            Render(context);
        }

        private void Render(MorningDayContext context)
        {
            Set(_dayLabel, $"День {context.Day}");
            Set(_titleLabel, context.Title);
            Set(_weatherLabel, $"Погода: {context.WeatherId}");
            Set(_summaryLabel, context.SummaryText);
            Set(_hintLabel, context.HintText);

            var modifiers = context.ActiveModifierIds;
            Set(_modifierLabel,
                modifiers is { Count: > 0 }
                    ? $"Спрос дня: {string.Join(", ", modifiers)}"
                    : "Спрос дня: нейтральный");
        }

        public async UniTask<bool> ContinueToPreparationAsync(CancellationToken ct)
        {
            if (_session == null)
            {
                Debug.LogWarning("[MorningScreenView] Cannot continue: IMorningSessionService is not injected.");
                return false;
            }

            var result = await _session.ContinueToPreparationAsync(ct);
            if (result == null)
                return false;

            Debug.Log($"[MorningScreenView] -> Preparation. Day {result.Day}, " +
                      $"modifiers=[{string.Join(",", result.ActiveModifierIds)}], " +
                      $"locations=[{string.Join(",", result.TargetLocationIds)}].");
            return true;
        }

        private static void Set(TMP_Text label, string value)
        {
            if (label != null)
                label.text = value ?? string.Empty;
        }

        private void OnDestroy()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
