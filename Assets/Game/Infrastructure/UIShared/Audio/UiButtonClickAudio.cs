using Infrastructure.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Plays a UI click sound when the attached <see cref="Button"/> is clicked. Drop on any button
    /// and assign the clip in the inspector. Uses the <see cref="Audio"/> facade, so it is a safe no-op
    /// until the audio service is bound at bootstrap. <see cref="Button.onClick"/> fires only when the
    /// button is interactable, so disabled buttons stay silent without extra checks.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class UiButtonClickAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip _click;
        [SerializeField, Range(0f, 1f)] private float _volumeScale = 1f;

        private Button _button;

        private void Awake() => _button = GetComponent<Button>();

        private void OnEnable() => _button.onClick.AddListener(Play);

        private void OnDisable() => _button.onClick.RemoveListener(Play);

        private void Play()
        {
            if (_click != null)
                Audio.PlayUi(_click, _volumeScale);
        }
    }
}
