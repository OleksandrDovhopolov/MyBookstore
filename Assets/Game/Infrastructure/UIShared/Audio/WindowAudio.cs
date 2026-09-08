using Infrastructure.Audio;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Plays open/close UI sounds tied to the window view's active lifecycle. <c>UIManager</c> activates
    /// the view GameObject on show and <see cref="WindowController{TView}.HideAsync"/> deactivates it on
    /// hide, so <see cref="OnEnable"/>/<see cref="OnDisable"/> map to window open/close. Place on the
    /// window view root and assign clips in the inspector.
    ///
    /// Notes:
    /// - The close sound fires when the GameObject is deactivated (after the hide animation completes).
    /// - <see cref="Audio"/> is null-safe: during app/scene teardown the facade is already cleared, so the
    ///   close sound is a no-op and no <c>AudioRoot</c> is resurrected.
    /// - Assumes the view is instantiated inactive and activated on show. If a prefab ships active, the
    ///   open sound would fire once on instantiation — keep window prefabs inactive at the root.
    /// </summary>
    public sealed class WindowAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip _open;
        [SerializeField] private AudioClip _close;

        private void OnEnable()
        {
            var clip = _open != null ? _open : Audio.Catalog?.WindowOpen;
            if (clip != null)
                Audio.PlayUi(clip);
        }

        private void OnDisable()
        {
            var clip = _close != null ? _close : Audio.Catalog?.WindowClose;
            if (clip != null)
                Audio.PlayUi(clip);
        }
    }
}
