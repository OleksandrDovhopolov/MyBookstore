using System;
using UnityEngine;

namespace Save.Identity
{
    // Генерирует UUID при первой установке, хранит в PlayerPrefs.
    // Используется HttpSaveStorage для идентификации игрока на сервере.
    public sealed class PersistentInstallPlayerIdentityProvider : IPlayerIdentityProvider
    {
        private const string PlayerIdPrefsKey = "save.http.player_id.v1";
        private const string LogPrefix = "[PlayerIdentityProvider]";

        private string _cachedPlayerId;

        public string GetPlayerId()
        {
            if (IsValid(_cachedPlayerId))
                return _cachedPlayerId;

            var storedPlayerId = PlayerPrefs.GetString(PlayerIdPrefsKey, string.Empty);
            if (IsValid(storedPlayerId))
            {
                _cachedPlayerId = storedPlayerId;
                AuthPlayerId.SeedIfMissing(_cachedPlayerId);
                return _cachedPlayerId;
            }

            _cachedPlayerId = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(PlayerIdPrefsKey, _cachedPlayerId);
            PlayerPrefs.Save();
            Debug.Log($"{LogPrefix} Generated new install player id.");

            // Until the anonymous auth flow exists, this install id IS the id the server knows, so
            // mirror it into auth.player_id.v1 for the editor tooling. SeedIfMissing never overwrites
            // an id a real auth flow already stored. Only on the resolve paths — the cached early-out
            // above would turn this into a PlayerPrefs read on every HTTP request.
            AuthPlayerId.SeedIfMissing(_cachedPlayerId);

            return _cachedPlayerId;
        }

        private static bool IsValid(string playerId) =>
            Guid.TryParseExact(playerId, "N", out _);
    }
}
