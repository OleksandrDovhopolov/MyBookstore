using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.Conditions.API;
using Game.Configs;
using Game.Configs.Models;
using Game.DayCycle.Day;
using Game.Quest.API;
using Game.Tutorial.API;
using Game.Tutorial.Steps;
using Game.UI;
using MessagePipe;
using Save;
using UnityEngine;

namespace Game.Tutorial.Services
{
    /// <summary>
    /// Layer 2 forced-step engine. Global singleton, mirrors QuestsService: registers as
    /// <see cref="ISaveHook"/> for init timing — the catalog is built in <see cref="AfterLoadAsync"/>
    /// (configs are warm by then), state is restored, triggers are subscribed. One exclusive sequence runs
    /// at a time; step handlers are resolved by type. Pass 1: handlers are stubs (log + auto-advance).
    /// One-way completion persisted in the <c>tutorial.state</c> save module.
    /// </summary>
    public sealed class TutorialService : ITutorialService, ISaveHook, IDisposable
    {
        private const string LogPrefix = "[Tutorial]";

        private readonly ISaveService _save;
        private readonly IConfigsService _configs;
        private readonly IConditionParser _parser;
        private readonly TutorialStepHandlerRegistry _handlers;

        // When false, sequences never auto-start from triggers or resume on load; explicit TryStartAsync still runs.
        private readonly bool _autoStart;

        private readonly ISubscriber<GameplayHubReady> _hubReadySub;
        private readonly IPublisher<TutorialSequenceStarted> _startedPub;
        private readonly IPublisher<TutorialStepChanged> _stepPub;
        private readonly IPublisher<TutorialSequenceCompleted> _completedPub;

        // Optional trigger/re-evaluate sources.
        private readonly IDayProgressService _dayProgress;
        private readonly IGameFlowService _gameFlow;
        private readonly IQuestsService _quests;
        private readonly IQuestReevaluationGate _questReevaluation;

        private readonly Dictionary<string, TutorialSequenceConfig> _sequences =
            new(StringComparer.Ordinal);
        private readonly List<TutorialSequenceConfig> _byPriority = new();

        private readonly List<IDisposable> _subscriptions = new();
        private readonly CancellationTokenSource _cts = new();

        private TutorialSaveState _state = new();
        private bool _loaded;
        private bool _running;
        private string _activeSequenceId;
        private CancellationTokenSource _runCts;

        public TutorialService(
            ISaveService save,
            IConfigsService configs,
            IConditionParser parser,
            TutorialStepHandlerRegistry handlers,
            ISubscriber<GameplayHubReady> hubReadySub,
            IPublisher<TutorialSequenceStarted> startedPub,
            IPublisher<TutorialStepChanged> stepPub,
            IPublisher<TutorialSequenceCompleted> completedPub,
            IDayProgressService dayProgress = null,
            IGameFlowService gameFlow = null,
            IQuestsService quests = null,
            IQuestReevaluationGate questReevaluation = null,
            bool autoStart = true)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
            _autoStart = autoStart;
            _hubReadySub = hubReadySub;
            _startedPub = startedPub;
            _stepPub = stepPub;
            _completedPub = completedPub;
            _dayProgress = dayProgress;
            _gameFlow = gameFlow;
            _quests = quests;
            _questReevaluation = questReevaluation;

            _save.RegisterHook(this);
        }

        public bool IsRunning => _running;
        public string ActiveSequenceId => _activeSequenceId;
        public IReadOnlyCollection<string> CompletedSequenceIds => _state.CompletedSequenceIds;

        public bool IsSequenceCompleted(string sequenceId)
            => sequenceId != null && _state.CompletedSequenceIds.Contains(sequenceId);

        // ----- ISaveHook -----

        public async UniTask AfterLoadAsync(CancellationToken ct)
        {
            _state = await _save.GetModuleAsync<TutorialSaveState>(TutorialSaveKeys.State, ct)
                     ?? new TutorialSaveState();
            _state.CompletedSequenceIds ??= new List<string>();

            BuildCatalog();
            Subscribe();
            _loaded = true;

            Debug.Log($"{LogPrefix} loaded: {_sequences.Count} sequences, " +
                      $"{_state.CompletedSequenceIds.Count} completed. autoStart={_autoStart}.");

            ResumeActiveSequence();
        }

        public UniTask BeforeSaveAsync(CancellationToken ct) => UniTask.CompletedTask;

        // ----- ITutorialService -----

        public UniTask<bool> TryStartAsync(string sequenceId, bool force, CancellationToken ct)
        {
            if (_running || sequenceId == null || !_sequences.TryGetValue(sequenceId, out var seq))
                return UniTask.FromResult(false);

            if (!force && (!ContextAllows(seq) || !IsEligible(seq)))
                return UniTask.FromResult(false);

            BeginRun(seq, startIndex: 0);
            return UniTask.FromResult(true);
        }

        public UniTask SkipActiveAsync(CancellationToken ct)
        {
            // Abort the active run WITHOUT marking complete; the activation scan can re-trigger it later.
            // Cancelling the run token unblocks a long-running step (showText/highlightClick) and lets its
            // finally-cleanup hide the overlay; the runner's finally clears _running/_activeSequenceId.
            _runCts?.Cancel();
            return UniTask.CompletedTask;
        }

        public async UniTask ResetAsync(string sequenceId, CancellationToken ct)
        {
            if (sequenceId == null) return;
            var changed = _state.CompletedSequenceIds.Remove(sequenceId);
            if (_state.ActiveSequenceId == sequenceId)
            {
                _state.ActiveSequenceId = null;
                _state.NextStepIndex = 0;
                changed = true;
            }
            if (changed) await PersistAsync(ct);
        }

        // ----- Catalog / subscriptions -----

        private void BuildCatalog()
        {
            _sequences.Clear();
            _byPriority.Clear();

            foreach (var cfg in _configs.GetAll<TutorialSequenceConfig>())
            {
                if (cfg == null || string.IsNullOrEmpty(cfg.Id)) continue;
                if (_sequences.ContainsKey(cfg.Id))
                {
                    Debug.LogError($"{LogPrefix} duplicate sequence id '{cfg.Id}', ignoring the later one.");
                    continue;
                }
                _sequences[cfg.Id] = cfg;
                _byPriority.Add(cfg);
            }

            _byPriority.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        private void Subscribe()
        {
            if (_hubReadySub != null)
                _subscriptions.Add(_hubReadySub.Subscribe(_ => OnTrigger(TutorialTriggers.HubReady, null)));

            if (_dayProgress != null)
                _dayProgress.PhaseChanged += OnPhaseChanged;

            if (_gameFlow != null)
                _gameFlow.LocationLoadedChanged += OnLocationLoadedChanged;

            if (_quests != null)
            {
                _quests.QuestStarted += OnQuestStarted;
                _quests.QuestCompleted += OnQuestCompleted;
            }
        }

        private void OnPhaseChanged(DayProgressState state)
            => OnTrigger(TutorialTriggers.PhaseChanged, state?.CurrentPhase.ToString());

        private void OnLocationLoadedChanged(bool loaded)
        {
            if (loaded) OnTrigger(TutorialTriggers.LocationLoaded, null);
        }

        private void OnQuestStarted(IQuest quest) => OnTrigger(TutorialTriggers.QuestStarted, quest?.Id);
        private void OnQuestCompleted(IQuest quest) => OnTrigger(TutorialTriggers.QuestCompleted, quest?.Id);

        // ----- Activation scan -----

        private void OnTrigger(string trigger, string param)
        {
            if (!_loaded || _running || !_autoStart) return;

            // Don't start a sequence mid-transition (overlay would appear under the transition cover).
            // Exception: locationLoaded fires DURING the transition (before reveal) — guarding it would
            // drop location sequences entirely.
            if (!string.Equals(trigger, TutorialTriggers.LocationLoaded, StringComparison.OrdinalIgnoreCase)
                && _gameFlow?.IsTransitioning == true)
                return;

            foreach (var seq in _byPriority)
            {
                if (!string.Equals(seq.Trigger, trigger, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrEmpty(seq.TriggerParam) &&
                    !string.Equals(seq.TriggerParam, param, StringComparison.Ordinal)) continue;
                if (!ContextAllows(seq)) continue;
                if (!IsEligible(seq)) continue;

                BeginRun(seq, startIndex: 0);
                return; // one exclusive runner
            }
        }

        private bool IsEligible(TutorialSequenceConfig seq)
        {
            if (_state.CompletedSequenceIds.Contains(seq.Id)) return false;
            return _parser.Parse(seq.ActivationConditions).Evaluate().IsMet;
        }

        // Activation gate only (NOT mid-run abort — a sequence gated to one context can still await events
        // that fire in another, e.g. a location sequence that waits for the Results window).
        private bool ContextAllows(TutorialSequenceConfig seq)
        {
            var inLocation = _gameFlow?.IsLocationLoaded ?? false;
            switch (seq.Context?.ToLowerInvariant())
            {
                case "hub": return !inLocation;
                case "location": return inLocation;
                default: return true; // "any" / null
            }
        }

        private void ResumeActiveSequence()
        {
            if (!_autoStart) return; // auto-start disabled: don't revive a mid-run sequence from a prior save
            if (_running) return; // a trigger may have already started a run during load
            var id = _state.ActiveSequenceId;
            if (string.IsNullOrEmpty(id)) return;
            if (!_sequences.TryGetValue(id, out var seq)) return;
            if (_state.CompletedSequenceIds.Contains(id)) return;

            var fromStep = string.Equals(seq.ResumePolicy, "fromStep", StringComparison.OrdinalIgnoreCase)
                ? Mathf.Clamp(_state.NextStepIndex, 0, seq.Steps?.Length ?? 0)
                : 0;

            BeginRun(seq, fromStep);
        }

        // ----- Runner -----

        private void BeginRun(TutorialSequenceConfig seq, int startIndex)
        {
            _running = true;
            _activeSequenceId = seq.Id;
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            RunSequenceAsync(seq, startIndex, _runCts.Token).Forget();
        }

        private async UniTaskVoid RunSequenceAsync(TutorialSequenceConfig seq, int startIndex, CancellationToken ct)
        {
            try
            {
                _startedPub?.Publish(new TutorialSequenceStarted(seq.Id));
                Debug.Log($"{LogPrefix} sequence '{seq.Id}' started at step {startIndex}.");

                var steps = seq.Steps ?? Array.Empty<TutorialStepConfig>();
                for (var i = startIndex; i < steps.Length; i++)
                {
                    var step = steps[i];

                    // Persist BEFORE running the step: quitting mid-step resumes THIS (not-yet-finished) step.
                    _state.ActiveSequenceId = seq.Id;
                    _state.NextStepIndex = i;
                    await PersistAsync(ct);

                    _stepPub?.Publish(new TutorialStepChanged(seq.Id, step?.Id, i));

                    if (_handlers.TryGet(step?.Type, out var handler))
                        await handler.ExecuteAsync(step, ct);
                    else
                        Debug.LogError($"{LogPrefix} no handler for step type '{step?.Type}' " +
                                       $"in '{seq.Id}' (step {i}); skipping.");
                }

                await CompleteAsync(seq, ct);
            }
            catch (OperationCanceledException)
            {
                // Torn down / aborted — do not mark complete.
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogPrefix} sequence '{seq.Id}' failed: {e}");
            }
            finally
            {
                _running = false;
                _activeSequenceId = null;
                _runCts?.Dispose();
                _runCts = null;
            }
        }

        private async UniTask CompleteAsync(TutorialSequenceConfig seq, CancellationToken ct)
        {
            if (!_state.CompletedSequenceIds.Contains(seq.Id))
                _state.CompletedSequenceIds.Add(seq.Id);
            _state.ActiveSequenceId = null;
            _state.NextStepIndex = 0;
            await PersistAsync(ct);

            _completedPub?.Publish(new TutorialSequenceCompleted(seq.Id));
            Debug.Log($"{LogPrefix} sequence '{seq.Id}' completed.");

            // Quests gating on "tutorialCompleted" are not driven by sales/decor/phase, so nudge a re-eval.
            _questReevaluation?.RequestReevaluation();
        }

        private UniTask PersistAsync(CancellationToken ct)
        {
            _state.UpdatedAtUtcIso = DateTime.UtcNow.ToString("o");
            return _save.UpdateModuleAsync(
                TutorialSaveKeys.State, _state, TutorialSaveKeys.StateSchemaVersion, ct);
        }

        public void Dispose()
        {
            foreach (var sub in _subscriptions) sub?.Dispose();
            _subscriptions.Clear();

            if (_dayProgress != null) _dayProgress.PhaseChanged -= OnPhaseChanged;
            if (_gameFlow != null) _gameFlow.LocationLoadedChanged -= OnLocationLoadedChanged;
            if (_quests != null)
            {
                _quests.QuestStarted -= OnQuestStarted;
                _quests.QuestCompleted -= OnQuestCompleted;
            }

            if (!_cts.IsCancellationRequested) _cts.Cancel();
            _cts.Dispose();
        }
    }
}
