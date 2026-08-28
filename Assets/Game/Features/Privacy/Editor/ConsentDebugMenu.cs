using Analytics;
using UnityEditor;
using UnityEngine;

namespace Game.Privacy.Editor
{
    /// <summary>
    /// Clears only the consent keys so the first-run gate can be re-tested. Unity's
    /// "Edit > Clear All PlayerPrefs" would also wipe audio settings, the analytics session number and the
    /// persistent player id used by the save server.
    /// </summary>
    public static class ConsentDebugMenu
    {
        [MenuItem("Tools/Privacy/Reset Consent")]
        public static void ResetConsent()
        {
            foreach (var key in PlayerPrefsConsentStore.AllKeys)
            {
                PlayerPrefs.DeleteKey(key);
            }

            PlayerPrefs.Save();
            Debug.Log("[Consent] Consent keys cleared. The privacy gate will show again on the next Play.");
        }
    }
}
