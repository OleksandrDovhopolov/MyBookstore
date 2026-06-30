using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Infrastructure.Audio.Tests.Editor
{
    public sealed class AudioServiceTests
    {
        [Test]
        public void PlaySfx_KeepsSourceVolumeAtOne()
        {
            var service = NewService();
            service.SetVolume(AudioChannelId.Master, 0.5f);
            service.SetVolume(AudioChannelId.Sfx, 0.5f);

            service.PlaySfx(CreateClip("sfx"));

            var root = GetRoot(service);
            Assert.NotNull(root);
            Assert.AreEqual(1f, root.SfxSource.volume);

            service.Dispose();
        }

        [Test]
        public void AmbientVolume_UpdatesWhilePlaying()
        {
            var service = NewService();
            service.SetVolume(AudioChannelId.Master, 0.5f);
            service.SetVolume(AudioChannelId.Ambient, 0.5f);

            service.PlayAmbient(CreateClip("ambient"));
            var root = GetRoot(service);
            Assert.AreEqual(0.25f, root.AmbientSource.volume, 0.0001f);

            service.SetVolume(AudioChannelId.Ambient, 0.8f);
            Assert.AreEqual(0.4f, root.AmbientSource.volume, 0.0001f);

            service.Dispose();
        }

        [Test]
        public void SetMuted_ZeroesLoopSourcesAndKeepsOneShotSourcesAtOne()
        {
            var service = NewService();
            service.PlayMusic(CreateClip("music"));
            service.PlayAmbient(CreateClip("ambient"));

            service.SetMuted(true);

            var root = GetRoot(service);
            Assert.AreEqual(0f, root.MusicSource.volume);
            Assert.AreEqual(0f, root.AmbientSource.volume);
            Assert.AreEqual(1f, root.SfxSource.volume);
            Assert.AreEqual(1f, root.UiSource.volume);

            service.Dispose();
        }

        [Test]
        public async Task PlayMusicAsync_SameAddress_UsesCachedClip()
        {
            var loader = new FakeClipLoader();
            var service = NewService(loader);

            await service.PlayMusicAsync("music/theme", CancellationToken.None);
            await service.PlayMusicAsync("music/theme", CancellationToken.None);

            Assert.AreEqual(1, loader.LoadCount);
            Assert.AreEqual(0, loader.ReleaseCount);

            service.Dispose();
        }

        [Test]
        public async Task PlayMusicAsync_ConcurrentSameAddress_ReleasesRaceLoser()
        {
            var loader = new FakeClipLoader { ManualCompletion = true };
            var service = NewService(loader);

            var first = service.PlayMusicAsync("music/theme", CancellationToken.None);
            var second = service.PlayMusicAsync("music/theme", CancellationToken.None);
            await UniTask.WaitUntil(() => loader.PendingCount == 2);

            var losingClip = CreateClip("loaded-first");
            var winningClip = CreateClip("loaded-second");
            loader.CompleteLoad(1, winningClip);
            await second;

            loader.CompleteLoad(0, losingClip);
            await first;

            Assert.AreEqual(2, loader.LoadCount);
            Assert.AreEqual(1, loader.ReleaseCount);
            Assert.AreSame(losingClip, loader.Released[0]);

            service.Dispose();
        }

        [Test]
        public async Task ReleaseCachedClips_AfterStop_ReleasesLoadedAddresses()
        {
            var loader = new FakeClipLoader();
            var service = NewService(loader);

            await service.PlayMusicAsync("music/theme", CancellationToken.None);
            service.StopMusic();
            service.ReleaseCachedClips();

            Assert.AreEqual(1, loader.ReleaseGroupCount);
            Assert.AreEqual("music/theme", loader.ReleasedGroups[0][0]);

            service.Dispose();
        }

        [Test]
        public async Task AudioFacade_AsyncWithoutService_Completes()
        {
            Audio.Clear();

            await Audio.PlayMusicAsync("music/theme", CancellationToken.None);
            await Audio.PlayAmbientAsync("ambient/room", CancellationToken.None);
            await Audio.PlaySfxAsync("sfx/click", CancellationToken.None);
            await Audio.PlayUiAsync("ui/click", CancellationToken.None);
        }

        [Test]
        public async Task Dispose_DuringAsyncLoad_DoesNotCreateRoot()
        {
            var loader = new FakeClipLoader { ManualCompletion = true };
            var service = NewService(loader);

            var play = service.PlayMusicAsync("music/slow", CancellationToken.None);
            await UniTask.WaitUntil(() => loader.PendingCount == 1);

            service.Dispose();
            await play;

            Assert.IsNull(GetRoot(service));
        }

        private static AudioService NewService(FakeClipLoader loader = null)
            => new(new FakeSettingsStore(), loader ?? new FakeClipLoader());

        private static AudioClip CreateClip(string name)
            => AudioClip.Create(name, 64, 1, 44100, false);

        private static AudioRoot GetRoot(AudioService service)
        {
            var field = typeof(AudioService).GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic);
            return (AudioRoot)field.GetValue(service);
        }

        private sealed class FakeSettingsStore : IAudioSettingsStore
        {
            public AudioVolumeSettings Settings = new();

            public AudioVolumeSettings Load() => Settings.Clone();

            public void Save(AudioVolumeSettings settings)
            {
                Settings = settings.Clone();
            }
        }

        private sealed class FakeClipLoader : IAudioClipLoader
        {
            private readonly List<UniTaskCompletionSource<AudioClip>> _pending = new();

            public bool ManualCompletion;
            public int LoadCount;
            public int ReleaseCount;
            public int ReleaseGroupCount;
            public readonly List<AudioClip> Released = new();
            public readonly List<string[]> ReleasedGroups = new();

            public int PendingCount => _pending.Count;

            public UniTask<AudioClip> LoadAsync(string address, CancellationToken ct)
            {
                LoadCount++;
                if (!ManualCompletion)
                    return UniTask.FromResult(CreateClip(address));

                var source = new UniTaskCompletionSource<AudioClip>();
                _pending.Add(source);
                ct.Register(() => source.TrySetCanceled(ct));
                return source.Task;
            }

            public void CompleteLoad(int index, AudioClip clip)
            {
                _pending[index].TrySetResult(clip);
            }

            public void Release(AudioClip clip)
            {
                ReleaseCount++;
                Released.Add(clip);
            }

            public void ReleaseGroup(IEnumerable<string> addresses)
            {
                ReleaseGroupCount++;
                ReleasedGroups.Add(new List<string>(addresses).ToArray());
            }
        }
    }
}
