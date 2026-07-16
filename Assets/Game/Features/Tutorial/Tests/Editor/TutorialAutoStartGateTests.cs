using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.Conditions.API;
using Game.Configs;
using Game.Configs.Models;
using Game.Tutorial.Services;
using Game.Tutorial.Steps;
using NUnit.Framework;
using Save;
using UnityEngine;

namespace Game.Tutorial.Tests.Editor
{
    public sealed class TutorialAutoStartGateTests
    {
        [Test]
        public async Task BlockedLocationLoaded_DoesNotStartImmediately()
        {
            var gate = new TutorialAutoStartGate();
            gate.Block();

            var gameFlow = new FakeGameFlow { IsLocationLoaded = true };
            var service = BuildService(gate, gameFlow, autoStart: true);
            try
            {
                await service.AfterLoadAsync(CancellationToken.None);

                gameFlow.RaiseLocationLoaded(true);

                Assert.IsFalse(service.IsRunning);
                Assert.IsNull(service.ActiveSequenceId);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public async Task Release_ReplaysDeferredLocationLoaded()
        {
            var gate = new TutorialAutoStartGate();
            gate.Block();

            var gameFlow = new FakeGameFlow { IsLocationLoaded = true };
            var service = BuildService(gate, gameFlow, autoStart: true);
            try
            {
                await service.AfterLoadAsync(CancellationToken.None);
                gameFlow.RaiseLocationLoaded(true);

                gate.Release();

                Assert.IsTrue(service.IsRunning);
                Assert.AreEqual("tutorial_day_1", service.ActiveSequenceId);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public async Task AutoStartFalse_DoesNotReplayDeferredTrigger()
        {
            var gate = new TutorialAutoStartGate();
            gate.Block();

            var gameFlow = new FakeGameFlow { IsLocationLoaded = true };
            var service = BuildService(gate, gameFlow, autoStart: false);
            try
            {
                await service.AfterLoadAsync(CancellationToken.None);
                gameFlow.RaiseLocationLoaded(true);

                gate.Release();

                Assert.IsFalse(service.IsRunning);
                Assert.IsNull(service.ActiveSequenceId);
            }
            finally
            {
                service.Dispose();
            }
        }

        private static TutorialService BuildService(
            TutorialAutoStartGate gate,
            FakeGameFlow gameFlow,
            bool autoStart)
        {
            var configs = new FakeConfigsService(new[]
            {
                new TutorialSequenceConfig
                {
                    Id = "tutorial_day_1",
                    Priority = 10,
                    Context = "location",
                    Trigger = TutorialTriggers.LocationLoaded,
                    Steps = new[]
                    {
                        new TutorialStepConfig { Id = "hold", Type = BlockingStepHandler.TypeId }
                    }
                }
            });

            return new TutorialService(
                new FakeSaveService(),
                configs,
                new AlwaysMetParser(),
                new TutorialStepHandlerRegistry(new ITutorialStepHandler[] { new BlockingStepHandler() }),
                hubReadySub: null,
                startedPub: null,
                stepPub: null,
                completedPub: null,
                gameFlow: gameFlow,
                autoStartGate: gate,
                autoStart: autoStart);
        }

        private sealed class BlockingStepHandler : ITutorialStepHandler
        {
            public const string TypeId = "hold";
            public string Type => TypeId;

            public async UniTask ExecuteAsync(TutorialStepConfig step, CancellationToken ct)
            {
                while (true)
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        private sealed class AlwaysMetParser : IConditionParser
        {
            public ICondition Parse(Newtonsoft.Json.Linq.JObject node) => new AlwaysMetCondition();
        }

        private sealed class AlwaysMetCondition : ICondition
        {
            public ConditionResult Evaluate() => ConditionResult.Boolean(true, "test.always");
        }

        private sealed class FakeConfigsService : IConfigsService
        {
            private readonly IReadOnlyList<TutorialSequenceConfig> _tutorials;

            public FakeConfigsService(IReadOnlyList<TutorialSequenceConfig> tutorials)
            {
                _tutorials = tutorials;
            }

            public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;

            public T Get<T>(string id) where T : class, IConfig
                => GetAll<T>().FirstOrDefault(c => c.Id == id);

            public bool TryGet<T>(string id, out T config) where T : class, IConfig
            {
                config = Get<T>(id);
                return config != null;
            }

            public UniTask<T> GetAsync<T>(string id) where T : class, IConfig
                => UniTask.FromResult(Get<T>(id));

            public bool IsExists<T>(string id) where T : class, IConfig => Get<T>(id) != null;

            public IReadOnlyList<T> GetAll<T>() where T : class, IConfig
                => typeof(T) == typeof(TutorialSequenceConfig)
                    ? _tutorials.Cast<T>().ToList()
                    : Array.Empty<T>();
        }

        private sealed class FakeSaveService : ISaveService
        {
            private readonly Dictionary<string, object> _modules = new();

            public UniTask LoadAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SaveAsync(CancellationToken ct, SaveMode mode = SaveMode.Regular) => UniTask.CompletedTask;

            public UniTask<T> GetModuleAsync<T>(string moduleKey, CancellationToken ct) where T : class
                => UniTask.FromResult(_modules.TryGetValue(moduleKey, out var value) ? value as T : null);

            public UniTask UpdateModuleAsync<T>(string moduleKey, T value, int schemaVersion, CancellationToken ct)
            {
                _modules[moduleKey] = value;
                return UniTask.CompletedTask;
            }

            public void MarkDirty() { }
            public void RegisterHook(ISaveHook hook) { }
            public IDisposable BlockAutosave() => new NoopLease();
            public void Dispose() { }

            private sealed class NoopLease : IDisposable
            {
                public void Dispose() { }
            }
        }

        private sealed class FakeGameFlow : IGameFlowService
        {
            public bool IsTransitioning { get; set; }
            public bool IsLocationLoaded { get; set; }

            public event Action<bool> LocationLoadedChanged;

            public void RegisterHubRoot(GameObject hubRoot) { }
            public UniTask EnterLocationAsync(CancellationToken ct = default) => UniTask.CompletedTask;
            public UniTask ReturnToHubAsync(CancellationToken ct = default) => UniTask.CompletedTask;

            public void RaiseLocationLoaded(bool loaded)
            {
                IsLocationLoaded = loaded;
                LocationLoadedChanged?.Invoke(loaded);
            }
        }
    }
}
