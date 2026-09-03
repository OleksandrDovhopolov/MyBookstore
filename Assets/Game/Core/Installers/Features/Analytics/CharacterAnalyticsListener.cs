using System;
using System.Collections.Generic;
using Game.Characters.API;
using VContainer.Unity;

namespace Game.Bootstrap.Analytics
{
    public sealed class CharacterAnalyticsListener : IStartable, IDisposable
    {
        private readonly ICharactersService _characters;
        private readonly global::Analytics.IAnalyticsService _analytics;

        public CharacterAnalyticsListener(ICharactersService characters, global::Analytics.IAnalyticsService analytics)
        {
            _characters = characters ?? throw new ArgumentNullException(nameof(characters));
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
        }

        public void Start()
        {
            _characters.CharacterDiscovered += OnCharacterDiscovered;
        }

        public void Dispose()
        {
            _characters.CharacterDiscovered -= OnCharacterDiscovered;
        }

        private void OnCharacterDiscovered(ICharacter character)
        {
            if (character == null) return;

            var parameters = new Dictionary<string, object>();
            AnalyticsParameterBag.AddString(parameters, global::Analytics.AnalyticsParameterNames.CharacterId, character.Id);

            _analytics.TrackEvent(new global::Analytics.AnalyticsEvent(
                global::Analytics.AnalyticsEventNames.CharacterDiscovered,
                parameters));
        }
    }
}
