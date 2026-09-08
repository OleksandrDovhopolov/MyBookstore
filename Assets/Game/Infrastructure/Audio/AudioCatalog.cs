using UnityEngine;

namespace Infrastructure.Audio
{
    [CreateAssetMenu(menuName = "Game/Audio/Audio Catalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        [Header("UI defaults")]
        [SerializeField] private AudioClip _buttonClick;
        [SerializeField] private AudioClip _windowOpen;
        [SerializeField] private AudioClip _windowClose;

        [Header("Music")]
        [SerializeField] private AudioClip _hubMusic;
        [SerializeField] private AudioClip[] _salesDayMusic;
        [SerializeField, Range(0f, 3f)] private float _musicFadeSeconds = 0.6f;

        public AudioClip ButtonClick => _buttonClick;
        public AudioClip WindowOpen => _windowOpen;
        public AudioClip WindowClose => _windowClose;
        public AudioClip HubMusic => _hubMusic;
        public float MusicFadeSeconds => _musicFadeSeconds;

        public AudioClip GetSalesDayMusic(int day)
        {
            if (_salesDayMusic == null || _salesDayMusic.Length == 0)
                return null;

            var zeroBasedDay = Mathf.Max(0, day - 1);
            return _salesDayMusic[zeroBasedDay % _salesDayMusic.Length];
        }
    }
}
