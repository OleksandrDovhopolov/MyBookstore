using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.DayCycle.Day;
using Game.Quest.API;
using Game.Tutorial;
using Game.Tutorial.API;
using Game.Tutorial.Presentation;
using Game.UI;
using MessagePipe;
using Save;
using UnityEngine;

namespace Game.Tutorial.Services
{
    /// <summary>
    /// Layer 2 forced-step engine. Global singleton, mirrors QuestsService: registers as
    /// <see cref="ISaveHook"/> for init timing, restores state, subscribes triggers, and runs one
    /// exclusive sequence at a time. Tutorial content is provided by DI-registered C# sequences.
    /// One-way completion persisted in the <c>tutorial.state</c> save module.
    /// </summary>
    public sealed class TutorialService : ITutorialService, ITutorialReevaluationGate, ISaveHook, IDisposable
    {
        private readonly ISaveService _save;
        private readonly IReadOnlyList<ITutorialSequence> _registeredSequences;
        private readonly ITutorialSettings _settings;

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
        private readonly ITutorialAutoStartGate _autoStartGate;
        private readonly IUIManager _ui;

        private readonly Dictionary<string, ITutorialSequence> _sequences =
            new(StringComparer.Ordinal);
        private readonly List<ITutorialSequence> _byPriority = new();

        private readonly List<IDisposable> _subscriptions = new();
        private readonly CancellationTokenSource _cts = new();

        private TutorialSaveState _state = new();
        private bool _loaded;
        private bool _running;
        private string _activeSequenceId;
        private CancellationTokenSource _runCts;
        private bool _rescanPending;

        public TutorialService(
            ISaveService save,
            IReadOnlyList<ITutorialSequence> sequences,
            ISubscriber<GameplayHubReady> hubReadySub,
            IPublisher<TutorialSequenceStarted> startedPub,
            IPublisher<TutorialStepChanged> stepPub,
            IPublisher<TutorialSequenceCompleted> completedPub,
            ITutorialSettings settings = null,
            IDayProgressService dayProgress = null,
            IGameFlowService gameFlow = null,
            IQuestsService quests = null,
            IQuestReevaluationGate questReevaluation = null,
            ITutorialAutoStartGate autoStartGate = null,
            IUIManager ui = null,
            bool autoStart = true)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _registeredSequences = sequences ?? Array.Empty<ITutorialSequence>();
            _settings = settings;
            _autoStart = autoStart;
            _hubReadySub = hubReadySub;
            _startedPub = startedPub;
            _stepPub = stepPub;
            _completedPub = completedPub;
            _dayProgress = dayProgress;
            _gameFlow = gameFlow;
            _quests = quests;
            _questReevaluation = questReevaluation;
            _autoStartGate = autoStartGate;
            _ui = ui;

            if (_autoStartGate != null)
                _autoStartGate.Released += OnAutoStartGateReleased;

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

            Debug.Log($"{TutorialLog.Prefix} loaded: {_sequences.Count} sequences, " +
                      $"{_state.CompletedSequenceIds.Count} completed. autoStart={_autoStart}.");

            ResumeActiveSequence();
        }

        public UniTask BeforeSaveAsync(CancellationToken ct) => UniTask.CompletedTask;

        // ----- ITutorialService -----

        public UniTask<bool> TryStartAsync(string sequenceId, bool force, CancellationToken ct)
        {
            if (_running || sequenceId == null || !_sequences.TryGetValue(sequenceId, out var seq))
                return UniTask.FromResult(false);

            if (!force)
            {
                if (!ContextAllows(seq) || !IsEligible(seq))
                    return UniTask.FromResult(false);

                if (!CanStartOverlay())
                {
                    _rescanPending = true;
                    return UniTask.FromResult(false);
                }
            }

            var steps = MaterializeSteps(seq);
            BeginRun(seq, steps, startIndex: 0);
            return UniTask.FromResult(true);
        }

        public UniTask SkipActiveAsync(CancellationToken ct)
        {
            // Abort the active run WITHOUT marking complete; the activation scan can re-trigger it later.
            // Cancelling the run token unblocks a long-running step and lets its finally-cleanup hide UI;
            // the runner's finally clears _running/_activeSequenceId.
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
                _state.NextStepId = null;
                changed = true;
            }
            if (changed) await PersistAsync(ct);
        }

        // ----- ITutorialReevaluationGate -----

        public void RequestReevaluation() => RequestRescan();

        // ----- Catalog / subscriptions -----

        private void BuildCatalog()
        {
            _sequences.Clear();
            _byPriority.Clear();

            var knownSequenceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var seq in _registeredSequences)
            {
                if (seq == null || string.IsNullOrEmpty(seq.Id)) continue;
                knownSequenceIds.Add(seq.Id);
            }

            ValidateSettings(knownSequenceIds);

            foreach (var seq in _registeredSequences)
            {
                if (seq == null || string.IsNullOrEmpty(seq.Id)) continue;
                if (_settings?.IsEnabled(seq.Id) == false)
                {
                    Debug.Log($"{TutorialLog.Prefix} sequence '{seq.Id}' disabled by settings.");
                    continue;
                }

                if (_sequences.ContainsKey(seq.Id))
                {
                    Debug.LogError($"{TutorialLog.Prefix} duplicate sequence id '{seq.Id}', ignoring the later one.");
                    continue;
                }
                _sequences[seq.Id] = seq;
                _byPriority.Add(seq);
            }

            _byPriority.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        private void ValidateSettings(IReadOnlyCollection<string> knownSequenceIds)
        {
            if (_settings == null)
                return;

            if (_settings is TutorialSettings tutorialSettings)
            {
                tutorialSettings.ValidateAgainst(knownSequenceIds);
                return;
            }

            var known = new HashSet<string>(knownSequenceIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in _settings.ConfiguredSequenceIds ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogWarning($"{TutorialLog.Prefix} tutorial settings contain an empty sequence id.");
                    continue;
                }

                if (!known.Contains(id))
                    Debug.LogWarning($"{TutorialLog.Prefix} tutorial settings contain unknown sequence id '{id}'.");

                if (!seen.Add(id))
                    Debug.LogWarning($"{TutorialLog.Prefix} tutorial settings contain duplicate sequence id '{id}'; first entry wins.");
            }
        }

        private void Subscribe()
        {
            if (_hubReadySub != null)
                _subscriptions.Add(_hubReadySub.Subscribe(_ => OnTrigger(TutorialTrigger.HubReady, null)));

            if (_dayProgress != null)
                _dayProgress.PhaseChanged += OnPhaseChanged;

            if (_gameFlow != null)
                _gameFlow.LocationLoadedChanged += OnLocationLoadedChanged;

            if (_quests != null)
            {
                _quests.QuestStarted += OnQuestStarted;
                _quests.QuestCompleted += OnQuestCompleted;
            }

            if (_ui != null)
                _ui.WindowHidden += OnWindowHidden;
        }

        private void OnPhaseChanged(DayProgressState state)
            => OnTrigger(TutorialTrigger.PhaseChanged, state?.CurrentPhase.ToString());

        private void OnLocationLoadedChanged(bool loaded)
        {
            if (loaded) OnTrigger(TutorialTrigger.LocationLoaded, null);
        }

        private void OnQuestStarted(IQuest quest) => OnTrigger(TutorialTrigger.QuestStarted, quest?.Id);
        private void OnQuestCompleted(IQuest quest) => OnTrigger(TutorialTrigger.QuestCompleted, quest?.Id);
        private void OnWindowHidden(IWindowController _) => TryConsumePendingRescan();

        // ----- Activation scan -----

        private void OnTrigger(TutorialTrigger trigger, string param)
        {
            if (!_loaded || _running || !_autoStart) return;

            if (_autoStartGate?.IsBlocked == true)
            {
                _rescanPending = true;
                return;
            }

            // Don't start a sequence mid-transition (overlay would appear under the transition cover).
            // Exception: locationLoaded fires DURING the transition (before reveal), so guarding it would
            // drop location sequences entirely.
            if (trigger != TutorialTrigger.LocationLoaded && _gameFlow?.IsTransitioning == true)
                return;

            if (!CanStartOverlay())
            {
                _rescanPending = true;
                return;
            }

            TryStartEligible();
        }

        private bool TryStartEligible()
        {
            foreach (var seq in _byPriority)
            {
                if (!ContextAllows(seq)) continue;
                if (!IsEligible(seq)) continue;

                var steps = MaterializeSteps(seq);
                BeginRun(seq, steps, startIndex: 0);
                return true; // one exclusive runner
            }

            return false;
        }

        private void RequestRescan()
        {
            if (!_loaded || _running || !_autoStart) return;

            if (_autoStartGate?.IsBlocked == true)
            {
                _rescanPending = true;
                return;
            }

            if (_gameFlow?.IsTransitioning == true)
                return;

            if (!CanStartOverlay())
            {
                _rescanPending = true;
                return;
            }

            TryStartEligible();
        }

        private void OnAutoStartGateReleased() => TryConsumePendingRescan();

        private void TryConsumePendingRescan()
        {
            if (!_rescanPending)
                return;

            _rescanPending = false;
            RequestRescan();
        }

        private bool IsEligible(ITutorialSequence seq)
        {
            if (_state.CompletedSequenceIds.Contains(seq.Id)) return false;
            return seq.IsEligible();
        }

        // Activation gate only (NOT mid-run abort - a sequence gated to one context can still await events
        // that fire in another, e.g. a location sequence that waits for the Results window).
        private bool ContextAllows(ITutorialSequence seq)
        {
            var inLocation = _gameFlow?.IsLocationLoaded ?? false;
            switch (seq.Context)
            {
                case TutorialContext.Hub: return !inLocation;
                case TutorialContext.Location: return inLocation;
                default: return true;
            }
        }

        private bool CanStartOverlay() => _ui == null || _ui.GetTopWindow() == null;

        private void ResumeActiveSequence()
        {
            if (!_autoStart) return; // auto-start disabled: don't revive a mid-run sequence from a prior save
            if (_running) return; // a trigger may have already started a run during load
            var id = _state.ActiveSequenceId;
            if (string.IsNullOrEmpty(id)) return;
            if (!_sequences.TryGetValue(id, out var seq)) return;
            if (_state.CompletedSequenceIds.Contains(id)) return;

            var steps = MaterializeSteps(seq);
            var fromStep = seq.ResumePolicy == TutorialResumePolicy.FromStep
                ? ResolveResumeIndex(steps)
                : 0;

            BeginRun(seq, steps, fromStep);
        }

        // ----- Runner -----

        private static IReadOnlyList<ITutorialStep> MaterializeSteps(ITutorialSequence seq)
            => seq.GetSteps() ?? Array.Empty<ITutorialStep>();

        private int ResolveResumeIndex(IReadOnlyList<ITutorialStep> steps)
        {
            if (!string.IsNullOrEmpty(_state.NextStepId))
            {
                for (var i = 0; i < steps.Count; i++)
                {
                    if (string.Equals(steps[i]?.Id, _state.NextStepId, StringComparison.Ordinal))
                        return i;
                }
            }

            return Mathf.Clamp(_state.NextStepIndex, 0, steps.Count);
        }

        private void BeginRun(ITutorialSequence seq, IReadOnlyList<ITutorialStep> steps, int startIndex)
        {
            _running = true;
            _activeSequenceId = seq.Id;
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            var clampedStartIndex = Mathf.Clamp(startIndex, 0, steps.Count);
            RunSequenceAsync(seq, steps, clampedStartIndex, _runCts.Token).Forget();
        }

        private async UniTaskVoid RunSequenceAsync(
            ITutorialSequence seq,
            IReadOnlyList<ITutorialStep> steps,
            int startIndex,
            CancellationToken ct)
        {
            var completed = false;
            try
            {
                _startedPub?.Publish(new TutorialSequenceStarted(seq.Id));
                Debug.Log($"{TutorialLog.Prefix} sequence '{seq.Id}' started at step {startIndex}.");
                seq.OnRunStarted();

                for (var i = startIndex; i < steps.Count; i++)
                {
                    var step = steps[i];
                    if (step == null)
                    {
                        Debug.LogError($"{TutorialLog.Prefix} null step in '{seq.Id}' (step {i}); skipping.");
                        continue;
                    }

                    // Persist BEFORE running the step: quitting mid-step resumes THIS not-yet-finished step.
                    _state.ActiveSequenceId = seq.Id;
                    _state.NextStepIndex = i;
                    _state.NextStepId = step.Id;
                    await PersistAsync(ct);

                    _stepPub?.Publish(new TutorialStepChanged(seq.Id, step.Id, i));
                    await step.ExecuteAsync(ct);
                }

                await CompleteAsync(seq, ct);
                completed = true;
            }
            catch (OperationCanceledException)
            {
                // Torn down / aborted - do not mark complete.
            }
            catch (Exception e)
            {
                Debug.LogError($"{TutorialLog.Prefix} sequence '{seq.Id}' failed: {e}");
            }
            finally
            {
                try
                {
                    seq.OnRunEnded();
                }
                catch (Exception e)
                {
                    Debug.LogError($"{TutorialLog.Prefix} sequence '{seq.Id}' teardown failed: {e}");
                }
                finally
                {
                    _running = false;
                    _activeSequenceId = null;
                    _runCts?.Dispose();
                    _runCts = null;

                    if (completed)
                        RequestRescan();
                }
            }
        }

        private async UniTask CompleteAsync(ITutorialSequence seq, CancellationToken ct)
        {
            if (!_state.CompletedSequenceIds.Contains(seq.Id))
                _state.CompletedSequenceIds.Add(seq.Id);
            _state.ActiveSequenceId = null;
            _state.NextStepIndex = 0;
            _state.NextStepId = null;
            await PersistAsync(ct);

            _completedPub?.Publish(new TutorialSequenceCompleted(seq.Id));
            Debug.Log($"{TutorialLog.Prefix} sequence '{seq.Id}' completed.");

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
            if (_autoStartGate != null) _autoStartGate.Released -= OnAutoStartGateReleased;
            if (_ui != null) _ui.WindowHidden -= OnWindowHidden;

            if (!_cts.IsCancellationRequested) _cts.Cancel();
            _cts.Dispose();
        }

    }
}
