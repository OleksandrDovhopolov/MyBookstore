using System;
using UnityEngine;

namespace Save.Identity
{
    /// <summary>
    /// The player id the server identifies this player by, cached in PlayerPrefs.
    ///
    /// Target state: written by the anonymous auth flow (`POST /api/v1/auth/anonymous`), which per
    /// docs/SERVICES/API_ENDPOINTS.md is only needed once backend auth becomes `Required`.
    ///
    /// Until that lands, the key is seeded from <see cref="PersistentInstallPlayerIdentityProvider"/>'s
    /// install id — see <see cref="SeedIfMissing"/>. That is not a guess: today `HttpSaveStorage` sends
    /// exactly that install id as the `playerId`, so it IS the id the server knows, and the future auth
    /// flow is specified to migrate it via `legacyPlayerId`.
    /// </summary>
    public static class AuthPlayerId
    {
        public const string PlayerPrefsKey = "auth.player_id.v1";

        private const string LogPrefix = "[AuthPlayerId]";

        /// <summary>GUID without dashes ("N" format), matching what the server issues.</summary>
        public static bool IsValid(string playerId) =>
            !string.IsNullOrWhiteSpace(playerId) && Guid.TryParseExact(playerId, "N", out _);

        /// <summary>Raw stored value, empty when nothing has written the key yet.</summary>
        public static string ReadRaw() => PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);

        public static bool TryRead(out string playerId)
        {
            playerId = ReadRaw();
            return IsValid(playerId);
        }

        /// <summary>
        /// Seeds the key from the legacy install id, but ONLY when it is still empty.
        ///
        /// The "only when empty" guard is the important part: once a real anonymous auth flow stores a
        /// server-issued id here, this must never overwrite it with the local install id — that would
        /// silently point every admin tool at the wrong player.
        /// </summary>
        public static void SeedIfMissing(string legacyPlayerId)
        {
            if (!IsValid(legacyPlayerId))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(ReadRaw()))
            {
                return;
            }

            PlayerPrefs.SetString(PlayerPrefsKey, legacyPlayerId);
            PlayerPrefs.Save();
            Debug.Log($"{LogPrefix} Seeded '{PlayerPrefsKey}' from the install id (no auth flow yet).");
        }
    }
}
