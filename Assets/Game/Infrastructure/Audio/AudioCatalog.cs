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

        [Header("Gameplay SFX")]
        [SerializeField] private AudioClip _purchaseSuccess;
        [SerializeField] private AudioClip _actionBlocked;
        [SerializeField] private AudioClip _decorPlace;
        [SerializeField] private AudioClip _decorRemove;
        [SerializeField] private AudioClip _currencyGained;
        [SerializeField] private AudioClip _bookSold;
        [SerializeField] private AudioClip _newJournalEntry;
        [SerializeField] private AudioClip _rewardReceived;
        [SerializeField] private AudioClip _locationDiscovered;
        [SerializeField] private AudioClip _dialogueLine;
        [SerializeField] private AudioClip _dayCompletionItem;

        [Header("Music")]
        [SerializeField] private AudioClip _hubMusic;
        [SerializeField] private AudioClip[] _salesDayMusic;
        [SerializeField, Range(0f, 3f)] private float _musicFadeSeconds = 0.6f;

        public AudioClip ButtonClick => _buttonClick;
        public AudioClip WindowOpen => _windowOpen;
        public AudioClip WindowClose => _windowClose;
        public AudioClip PurchaseSuccess => _purchaseSuccess;
        public AudioClip ActionBlocked => _actionBlocked;
        public AudioClip DecorPlace => _decorPlace;
        public AudioClip DecorRemove => _decorRemove;
        public AudioClip CurrencyGained => _currencyGained;
        public AudioClip BookSold => _bookSold;
        public AudioClip NewJournalEntry => _newJournalEntry;
        public AudioClip RewardReceived => _rewardReceived;
        public AudioClip LocationDiscovered => _locationDiscovered;
        public AudioClip DialogueLine => _dialogueLine;
        public AudioClip DayCompletionItem => _dayCompletionItem;
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
