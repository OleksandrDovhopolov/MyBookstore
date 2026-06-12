using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.DayCycle.Morning.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.DayCycle.Morning.UI
{
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

        [Header("Continue")]
        [SerializeField] private Button _continueButton;

        [Header("Next screen")]
        [SerializeField] private GameObject _preparationScreenRoot;

        private IMorningSessionService _session;
        private readonly CancellationTokenSource _cts = new();

        [Inject]
        public void Construct(IMorningSessionService session)
        {
            _session = session;
        }

        private void Awake()
        {
            if (_continueButton != null)
                _continueButton.onClick.AddListener(OnContinueClicked);
        }

        private void Start()
        {
            if (_session == null)
            {
                Debug.LogWarning("[MorningScreenView] IMorningSessionService не внедрён — экран не зарегистрирован в DI?");
                return;
            }

            RefreshAsync(_cts.Token).Forget();
        }

        private async UniTaskVoid RefreshAsync(CancellationToken ct)
        {
            SetInteractable(false);
            var context = await _session.StartOrResumeAsync(ct);
            Render(context);
            SetInteractable(true);
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

        private void OnContinueClicked()
        {
            if (_session == null) return;
            ContinueAsync(_cts.Token).Forget();
        }

        private async UniTaskVoid ContinueAsync(CancellationToken ct)
        {
            SetInteractable(false);
            var result = await _session.ContinueToPreparationAsync(ct);

            Debug.Log($"[MorningScreenView] → Подготовка. День {result.Day}, " +
                      $"модификаторы=[{string.Join(",", result.ActiveModifierIds)}], " +
                      $"локации=[{string.Join(",", result.TargetLocationIds)}].");

            if (_preparationScreenRoot != null) _preparationScreenRoot.SetActive(true);
            gameObject.SetActive(false);
        }

        private void SetInteractable(bool value)
        {
            if (_continueButton != null)
                _continueButton.interactable = value;
        }

        private static void Set(TMP_Text label, string value)
        {
            if (label != null)
                label.text = value ?? string.Empty;
        }

        private void OnDestroy()
        {
            if (_continueButton != null)
                _continueButton.onClick.RemoveListener(OnContinueClicked);

            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
