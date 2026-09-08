using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.DayCycle.Day;
using Infrastructure.Audio;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    public sealed class MusicDirector : IStartable, IDisposable
    {
        private readonly IAudioService _audio;
        private readonly IGameFlowService _gameFlow;
        private readonly IDayProgressService _dayProgress;
        private readonly AudioCatalog _catalog;
        private readonly CancellationTokenSource _cts = new();

        private AudioClip _lastRequested;
        private bool _started;
        private bool _disposed;

        public MusicDirector(
            IAudioService audio,
            IGameFlowService gameFlow,
            IDayProgressService dayProgress,
            AudioCatalog catalog = null)
        {
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _gameFlow = gameFlow ?? throw new ArgumentNullException(nameof(gameFlow));
            _dayProgress = dayProgress ?? throw new ArgumentNullException(nameof(dayProgress));
            _catalog = catalog;
        }

        public void Start()
        {
            if (_started || _disposed) return;

            _gameFlow.LocationLoadedChanged += OnLocationLoadedChanged;
            _dayProgress.PhaseChanged += OnPhaseChanged;
            _started = true;
            Apply();
        }

        public void Dispose()
        {
            if (_disposed) return;

            if (_started)
            {
                _gameFlow.LocationLoadedChanged -= OnLocationLoadedChanged;
                _dayProgress.PhaseChanged -= OnPhaseChanged;
            }

            _cts.Cancel();
            _cts.Dispose();
            _started = false;
            _disposed = true;
        }

        private void OnLocationLoadedChanged(bool _) => Apply();
        private void OnPhaseChanged(DayProgressState _) => Apply();

        private void Apply()
        {
            if (_catalog == null) return;

            var clip = _gameFlow.IsLocationLoaded
                ? _catalog.GetSalesDayMusic(_dayProgress.Current?.CurrentDay ?? 1)
                : _catalog.HubMusic;

            if (clip == _lastRequested) return;

            _lastRequested = clip;
            _audio.PlayMusicFadedAsync(clip, _catalog.MusicFadeSeconds, _cts.Token).Forget();
        }
    }
}
