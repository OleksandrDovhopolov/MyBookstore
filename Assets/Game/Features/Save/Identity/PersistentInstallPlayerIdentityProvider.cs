using System;
using UnityEngine;

namespace Save.Identity
{
    // Источник: Research/PersistentInstallPlayerIdentityProvider.cs — без изменений логики.
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
                return _cachedPlayerId;
            }

            _cachedPlayerId = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(PlayerIdPrefsKey, _cachedPlayerId);
            PlayerPrefs.Save();
            Debug.Log($"{LogPrefix} Generated new install player id.");

            return _cachedPlayerId;
        }

        private static bool IsValid(string playerId) =>
            Guid.TryParseExact(playerId, "N", out _);
    }
}
