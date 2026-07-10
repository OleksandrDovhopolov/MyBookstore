using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Infrastructure.Audio
{
    /// <summary>
    /// Thin facade for small MonoBehaviours that cannot receive constructor injection.
    /// Domain services should still depend on <see cref="IAudioService"/> directly.
    /// </summary>
    public static class Audio
    {
        private static IAudioService _service;

        public static bool IsAvailable => _service != null;

        public static void Bind(IAudioService service)
        {
            _service = service;
        }

        public static void Clear(IAudioService service = null)
        {
            if (service == null || ReferenceEquals(_service, service))
                _service = null;
        }

        public static void SetVolume(AudioChannelId channel, float volume)
        {
            _service?.SetVolume(channel, volume);
        }

        public static float GetVolume(AudioChannelId channel)
        {
            return _service?.GetVolume(channel) ?? 1f;
        }

        public static void PlayMusic(AudioClip clip, bool loop = true, bool restartIfSame = false)
        {
            _service?.PlayMusic(clip, loop, restartIfSame);
        }

        public static UniTask PlayMusicAsync(
            string address, CancellationToken ct, bool loop = true, bool restartIfSame = false)
        {
            return _service?.PlayMusicAsync(address, ct, loop, restartIfSame) ?? UniTask.CompletedTask;
        }

        public static void StopMusic()
        {
            _service?.StopMusic();
        }

        public static void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            _service?.PlaySfx(clip, volumeScale);
        }

        public static UniTask PlaySfxAsync(string address, CancellationToken ct, float volumeScale = 1f)
        {
            return _service?.PlaySfxAsync(address, ct, volumeScale) ?? UniTask.CompletedTask;
        }

        public static void PlaySfxAt(AudioClip clip, Vector3 position, float volumeScale = 1f)
        {
            _service?.PlaySfxAt(clip, position, volumeScale);
        }

        public static void PlayUi(AudioClip clip, float volumeScale = 1f)
        {
            _service?.PlayUi(clip, volumeScale);
        }

        public static UniTask PlayUiAsync(string address, CancellationToken ct, float volumeScale = 1f)
        {
            return _service?.PlayUiAsync(address, ct, volumeScale) ?? UniTask.CompletedTask;
        }

        public static void PlayAmbient(AudioClip clip, bool loop = true, bool restartIfSame = false)
        {
            _service?.PlayAmbient(clip, loop, restartIfSame);
        }

        public static UniTask PlayAmbientAsync(
            string address, CancellationToken ct, bool loop = true, bool restartIfSame = false)
        {
            return _service?.PlayAmbientAsync(address, ct, loop, restartIfSame) ?? UniTask.CompletedTask;
        }

        public static void StopAmbient()
        {
            _service?.StopAmbient();
        }

        public static void StopSfx()
        {
            _service?.StopSfx();
        }

        public static void StopAll()
        {
            _service?.StopAll();
        }

        public static void ReleaseCachedClips()
        {
            _service?.ReleaseCachedClips();
        }

        public static void SetMuted(bool muted)
        {
            _service?.SetMuted(muted);
        }
    }
}
