using System;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Ftue
{
    /// <summary>
    /// View for the welcome letter window. Two clickable elements only:
    /// <list type="number">
    /// <item><see cref="LetterClick"/> — the envelope/letter catcher (step 3); enabled once the idle is reached.</item>
    /// <item><see cref="StartClick"/> — the Start button (step 5); enabled only after the letter-open clip.</item>
    /// </list>
    /// No close/back button — Start is the only exit. Letter text is static (set on the prefab).
    /// </summary>
    public class WelcomeWindowView : WindowView
    {
        public event Action LetterClick;
        public event Action StartClick;

        [SerializeField] private Button _letterButton;
        [SerializeField] private Button _startButton;
        [SerializeField] private TextMeshProUGUI _letterText;
        [SerializeField] private WelcomeWindowAnimation _welcomeAnimation;
        
        public WelcomeWindowAnimation WelcomeAnimation => _welcomeAnimation;

        protected override void Awake()
        {
            base.Awake();

            if (_letterButton != null)
                _letterButton.onClick.AddListener(() => LetterClick?.Invoke());
            if (_startButton != null)
                _startButton.onClick.AddListener(() => StartClick?.Invoke());
        }

        public void SetLetterInteractable(bool interactable)
        {
            if (_letterButton != null) _letterButton.interactable = interactable;
        }

        public void SetStartInteractable(bool interactable)
        {
            if (_startButton != null) _startButton.interactable = interactable;
        }

        /// <summary>Optional override of the static letter text (text is normally authored on the prefab).</summary>
        public void SetText(string text)
        {
            if (_letterText != null) _letterText.text = text;
        }
    }
}
