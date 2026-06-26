using System;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Ftue
{
    public class WelcomeWindowView : WindowView
    {
        public event Action ClaimClick;
        
        [SerializeField] private Button _claimButton;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        
        protected override void Awake()
        {
            base.Awake();
            
            _claimButton.onClick.AddListener(() => ClaimClick?.Invoke());
        }
        
        public void Setup(string description)
        {
            //_descriptionText.text = LocalizationManager.Instance.Translate(description);
            _descriptionText.text = description;
        }
    }
}
