using System;
using DG.Tweening;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameplayUI
{
    public class UISwitch : MonoBehaviour
    {
        [SerializeField] private Image _bgImage;

        [SerializeField] private Sprite _activeBG;
        [SerializeField] private Sprite _inactiveBG;
        [SerializeField] private Image _switchPointer;
        [SerializeField] private UIBehaviour _button;
        [SerializeField] private Sprite _activePointer;
        [SerializeField] private Sprite _inactivePointer;
        [SerializeField] private TMP_Text _activeText;
        [SerializeField] private TMP_Text _inactiveText;
        

        private const float OffsetX = 40f;
        
        private float _startX;
        private bool _isActive;

        private Action<bool> _setter;
        private Func<bool> _getter;

        private void Awake()
        {
            _startX = _switchPointer.transform.localPosition.x;
            _button.gameObject.OnPointerUpAsObservable(Switch);
        }

        private void OnDestroy()
        {
            _button.gameObject.UnsubscribeOnPointerUpAsObservable(Switch);
        }

        public void Init(Action<bool> setter, Func<bool> getter)
        {
            _setter = setter;
            _getter = getter;
        }

        private void Switch()
        {
            _isActive = !_isActive;

            UpdateVisual(true);
            
            _setter?.Invoke(_isActive);
        }

        public void LoadState()
        {
            _isActive = _getter?.Invoke() ?? false;
            UpdateVisual(false);
        }

        public void SetIsOnWithoutNotify(bool isActive)
        {
            _isActive = isActive;
            UpdateVisual(false);
        }

        public void SetLabel(string inactiveString, string activeString)
        {
            if (_activeText != null && _inactiveText != null)
            {
                _activeText.text = activeString;
                _inactiveText.text = inactiveString;
            }
        }
        
        private void UpdateVisual(bool isAnimated)
        {
            _bgImage.sprite = _isActive ? _activeBG : _inactiveBG;
            _switchPointer.sprite = _isActive ? _activePointer : _inactivePointer;
            _switchPointer.DOKill();
            if (isAnimated)
                _switchPointer.transform.DOLocalMoveX(_isActive ? _startX + OffsetX : _startX, .05f).SetUpdate(true);
            else
                _switchPointer.transform.localPosition = _switchPointer.transform.localPosition.ReplaceX(_isActive ? _startX + OffsetX : _startX);

            if (_activeText != null && _inactiveText != null)
            {
                _activeText.gameObject.SetActive(_isActive);
                _inactiveText.gameObject.SetActive(!_isActive);
            }
        }
    }
}
