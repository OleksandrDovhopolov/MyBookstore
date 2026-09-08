using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.DayCycle.Day;
using Infrastructure.Audio;
using NUnit.Framework;
using UnityEngine;

namespace Game.Bootstrap.Tests.Editor
{
    public sealed class MusicDirectorTests
    {
        [Test]
        public void Start_OutsideLocation_RequestsHubMusic()
        {
            var hub = CreateClip("hub");
            var director = CreateDirector(CreateCatalog(hub, Array.Empty<AudioClip>()), out var audio, out _, out _);

            director.Start();

            Assert.AreSame(hub, audio.RequestedClips[0]);
            director.Dispose();
        }

        [Test]
        public void LocationEntry_AlternatesSalesMusicByCurrentDay()
        {
            var day1 = CreateClip("day1");
            var day2 = CreateClip("day2");
            var director = CreateDirector(CreateCatalog(null, new[] { day1, day2 }), out var audio, out var flow, out var day);

            director.Start();
            flow.RaiseLocationLoaded(true);
            day.Current.CurrentDay = 2;
            day.RaisePhaseChanged();
            day.Current.CurrentDay = 3;
            day.RaisePhaseChanged();

            CollectionAssert.AreEqual(new[] { day1, day2, day1 }, audio.RequestedClips);
            director.Dispose();
        }

        [Test]
        public void RepeatedLocationLoadedWithSameClip_DoesNotRequestTwice()
        {
            var day1 = CreateClip("day1");
            var director = CreateDirector(CreateCatalog(null, new[] { day1 }), out var audio, out var flow, out _);

            director.Start();
            flow.RaiseLocationLoaded(true);
            flow.RaiseLocationLoaded(true);

            Assert.AreEqual(1, audio.RequestedClips.Count);
            Assert.AreSame(day1, audio.RequestedClips[0]);
            director.Dispose();
        }

        [Test]
        public void FailedEntrySequence_ReturnsToHubMusic()
        {
            var hub = CreateClip("hub");
            var day1 = CreateClip("day1");
            var director = CreateDirector(CreateCatalog(hub, new[] { day1 }), out var audio, out var flow, out _);

            director.Start();
            flow.RaiseLocationLoaded(true);
            flow.RaiseLocationLoaded(false);

            CollectionAssert.AreEqual(new[] { hub, day1, hub }, audio.RequestedClips);
            director.Dispose();
        }

        [Test]
        public void EmptyCatalog_DoesNotRequestMusic()
        {
            var director = CreateDirector(CreateCatalog(null, Array.Empty<AudioClip>()), out var audio, out var flow, out var day);

            director.Start();
            flow.RaiseLocationLoaded(true);
            day.Current.CurrentDay = 2;
            day.RaisePhaseChanged();

            Assert.AreEqual(0, audio.RequestedClips.Count);
            director.Dispose();
        }

        [Test]
        public void Dispose_UnsubscribesFromEvents()
        {
            var day1 = CreateClip("day1");
            var director = CreateDirector(CreateCatalog(null, new[] { day1 }), out var audio, out var flow, out _);

            director.Start();
            director.Dispose();
            flow.RaiseLocationLoaded(true);

            Assert.AreEqual(0, audio.RequestedClips.Count);
        }

        private static MusicDirector CreateDirector(
            AudioCatalog catalog,
            out FakeAudioService audio,
            out FakeGameFlowService flow,
            out FakeDayProgressService day)
        {
            audio = new FakeAudioService();
            flow = new FakeGameFlowService();
            day = new FakeDayProgressService();
            return new MusicDirector(audio, flow, day, catalog);
        }

        private static AudioCatalog CreateCatalog(AudioClip hub, AudioClip[] sales)
        {
            var catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            SetPrivateField(catalog, "_hubMusic", hub);
            SetPrivateField(catalog, "_salesDayMusic", sales);
            SetPrivateField(catalog, "_musicFadeSeconds", 0f);
            return catalog;
        }

        private static AudioClip CreateClip(string name)
            => AudioClip.Create(name, 64, 1, 44100, false);

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }

        private sealed class FakeAudioService : IAudioService
        {
            public readonly List<AudioClip> RequestedClips = new();

            public AudioVolumeSettings Volumes => new();
            public bool IsMusicPlaying => RequestedClips.Count > 0;

            public void SetVolume(AudioChannelId channel, float volume) { }
            public float GetVolume(AudioChannelId channel) => 1f;
            public void PlayMusic(AudioClip clip, bool loop = true, bool restartIfSame = false) => RequestedClips.Add(clip);
            public UniTask PlayMusicFadedAsync(AudioClip clip, float fadeSeconds, CancellationToken ct, bool loop = true)
            {
                RequestedClips.Add(clip);
                return UniTask.CompletedTask;
            }
            public UniTask PlayMusicAsync(string address, CancellationToken ct, bool loop = true, bool restartIfSame = false) => UniTask.CompletedTask;
            public void StopMusic() { }
            public void PlaySfx(AudioClip clip, float volumeScale = 1f) { }
            public UniTask PlaySfxAsync(string address, CancellationToken ct, float volumeScale = 1f) => UniTask.CompletedTask;
            public void PlaySfxAt(AudioClip clip, Vector3 position, float volumeScale = 1f) { }
            public void PlayUi(AudioClip clip, float volumeScale = 1f) { }
            public UniTask PlayUiAsync(string address, CancellationToken ct, float volumeScale = 1f) => UniTask.CompletedTask;
            public void PlayAmbient(AudioClip clip, bool loop = true, bool restartIfSame = false) { }
            public UniTask PlayAmbientAsync(string address, CancellationToken ct, bool loop = true, bool restartIfSame = false) => UniTask.CompletedTask;
            public void StopSfx() { }
            public void StopAmbient() { }
            public void StopAll() { }
            public void ReleaseCachedClips() { }
            public void SetMuted(bool muted) { }
        }

        private sealed class FakeGameFlowService : IGameFlowService
        {
            public bool IsTransitioning => false;
            public bool IsLocationLoaded { get; private set; }
            public event Action<bool> LocationLoadedChanged;

            public void RegisterHubRoot(GameObject hubRoot) { }
            public UniTask EnterLocationAsync(string locationId, CancellationToken ct = default) => UniTask.CompletedTask;
            public UniTask ReturnToHubAsync(CancellationToken ct = default) => UniTask.CompletedTask;

            public void RaiseLocationLoaded(bool loaded)
            {
                IsLocationLoaded = loaded;
                LocationLoadedChanged?.Invoke(loaded);
            }
        }

        private sealed class FakeDayProgressService : IDayProgressService
        {
            public event Action<DayProgressState> PhaseChanged;
            public DayProgressState Current { get; } = new();

            public UniTask<DayProgressState> LoadAsync(CancellationToken ct) => UniTask.FromResult(Current);
            public UniTask SetPhaseAsync(DayPhase phase, CancellationToken ct)
            {
                Current.CurrentPhase = phase;
                RaisePhaseChanged();
                return UniTask.CompletedTask;
            }
            public UniTask MarkCurrentDayCompletedAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask AdvanceToNextDayAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SaveAsync(CancellationToken ct) => UniTask.CompletedTask;

            public void RaisePhaseChanged() => PhaseChanged?.Invoke(Current);
        }
    }
}
