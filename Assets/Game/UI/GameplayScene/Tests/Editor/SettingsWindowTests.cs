using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Infrastructure.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GameplayUI.Tests.Editor
{
    public sealed class SettingsWindowTests
    {
        [Test]
        public void SoundToggle_Off_WritesSfxAndUiToZero()
        {
            var audio = new FakeAudioService();

            SettingsAudioToggleAdapter.SetSoundEnabled(audio, false);

            Assert.AreEqual(0f, audio.GetVolume(AudioChannelId.Sfx));
            Assert.AreEqual(0f, audio.GetVolume(AudioChannelId.Ui));
            CollectionAssert.AreEqual(
                new[] { AudioChannelId.Sfx, AudioChannelId.Ui },
                audio.WrittenChannels);
        }

        [Test]
        public void SoundToggle_On_WritesSfxAndUiToOne()
        {
            var audio = new FakeAudioService();
            audio.SetVolume(AudioChannelId.Sfx, 0f);
            audio.SetVolume(AudioChannelId.Ui, 0f);
            audio.WrittenChannels.Clear();

            SettingsAudioToggleAdapter.SetSoundEnabled(audio, true);

            Assert.AreEqual(1f, audio.GetVolume(AudioChannelId.Sfx));
            Assert.AreEqual(1f, audio.GetVolume(AudioChannelId.Ui));
            CollectionAssert.AreEqual(
                new[] { AudioChannelId.Sfx, AudioChannelId.Ui },
                audio.WrittenChannels);
        }

        [Test]
        public void MusicToggle_WritesMusicAndAmbient()
        {
            var audio = new FakeAudioService();

            SettingsAudioToggleAdapter.SetMusicEnabled(audio, false);
            SettingsAudioToggleAdapter.SetMusicEnabled(audio, true);

            CollectionAssert.AreEqual(
                new[] { AudioChannelId.Music, AudioChannelId.Ambient, AudioChannelId.Music, AudioChannelId.Ambient },
                audio.WrittenChannels);
            Assert.AreEqual(1f, audio.GetVolume(AudioChannelId.Music));
            Assert.AreEqual(1f, audio.GetVolume(AudioChannelId.Ambient));
        }

        [Test]
        public void SoundEnabled_IsTrueWhenEitherSfxOrUiIsAudible()
        {
            var audio = new FakeAudioService();

            audio.SetVolume(AudioChannelId.Sfx, 0f);
            audio.SetVolume(AudioChannelId.Ui, 0f);
            Assert.IsFalse(SettingsAudioToggleAdapter.IsSoundEnabled(audio));

            audio.SetVolume(AudioChannelId.Sfx, 1f);
            audio.SetVolume(AudioChannelId.Ui, 0f);
            Assert.IsTrue(SettingsAudioToggleAdapter.IsSoundEnabled(audio));

            audio.SetVolume(AudioChannelId.Sfx, 0f);
            audio.SetVolume(AudioChannelId.Ui, 1f);
            Assert.IsTrue(SettingsAudioToggleAdapter.IsSoundEnabled(audio));
        }

        [Test]
        public void MusicEnabled_IsTrueWhenEitherMusicOrAmbientIsAudible()
        {
            var audio = new FakeAudioService();

            audio.SetVolume(AudioChannelId.Music, 0f);
            audio.SetVolume(AudioChannelId.Ambient, 0f);
            Assert.IsFalse(SettingsAudioToggleAdapter.IsMusicEnabled(audio));

            audio.SetVolume(AudioChannelId.Music, 1f);
            audio.SetVolume(AudioChannelId.Ambient, 0f);
            Assert.IsTrue(SettingsAudioToggleAdapter.IsMusicEnabled(audio));

            audio.SetVolume(AudioChannelId.Music, 0f);
            audio.SetVolume(AudioChannelId.Ambient, 1f);
            Assert.IsTrue(SettingsAudioToggleAdapter.IsMusicEnabled(audio));
        }

        [Test]
        public void SetSoundAndMusic_DoNotFireSwitchEvents()
        {
            var root = new GameObject("SettingsWindowViewTests");
            root.SetActive(false);
            try
            {
                var view = root.AddComponent<SettingsWindowView>();
                var soundSwitch = CreateChildSwitch(root.transform, "Sound");
                var musicSwitch = CreateChildSwitch(root.transform, "Music");
                SetPrivateField(view, "_soundSwitch", soundSwitch);
                SetPrivateField(view, "_musicSwitch", musicSwitch);

                root.SetActive(true);

                var soundChanges = 0;
                var musicChanges = 0;
                view.SoundChanged += _ => soundChanges++;
                view.MusicChanged += _ => musicChanges++;

                view.SetSound(true);
                view.SetMusic(true);
                view.SetSound(false);
                view.SetMusic(false);

                Assert.AreEqual(0, soundChanges);
                Assert.AreEqual(0, musicChanges);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static UISwitch CreateChildSwitch(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            var bgImage = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            var pointer = new GameObject("Pointer").AddComponent<Image>();
            pointer.transform.SetParent(go.transform);

            var uiSwitch = go.AddComponent<UISwitch>();
            SetPrivateField(uiSwitch, "_bgImage", bgImage);
            SetPrivateField(uiSwitch, "_switchPointer", pointer);
            SetPrivateField(uiSwitch, "_button", button);

            return uiSwitch;
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }

        private sealed class FakeAudioService : IAudioService
        {
            private readonly Dictionary<AudioChannelId, float> _volumes = new()
            {
                [AudioChannelId.Master] = 1f,
                [AudioChannelId.Music] = 1f,
                [AudioChannelId.Sfx] = 1f,
                [AudioChannelId.Ui] = 1f,
                [AudioChannelId.Ambient] = 1f,
            };

            public readonly List<AudioChannelId> WrittenChannels = new();

            public AudioVolumeSettings Volumes => new()
            {
                Master = GetVolume(AudioChannelId.Master),
                Music = GetVolume(AudioChannelId.Music),
                Sfx = GetVolume(AudioChannelId.Sfx),
                Ui = GetVolume(AudioChannelId.Ui),
                Ambient = GetVolume(AudioChannelId.Ambient)
            };

            public bool IsMusicPlaying => false;

            public void SetVolume(AudioChannelId channel, float volume)
            {
                _volumes[channel] = volume;
                WrittenChannels.Add(channel);
            }

            public float GetVolume(AudioChannelId channel)
                => _volumes.TryGetValue(channel, out var volume) ? volume : 0f;

            public void PlayMusic(AudioClip clip, bool loop = true, bool restartIfSame = false) { }
            public UniTask PlayMusicFadedAsync(AudioClip clip, float fadeSeconds, CancellationToken ct, bool loop = true) => UniTask.CompletedTask;
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
    }
}
