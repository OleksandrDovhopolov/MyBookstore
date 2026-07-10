using System;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Infrastructure
{
    public class ButtonAnimation : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private float _duration = 0.18f;
        [SerializeField] private RectTransform _animationRoot;
        
        private Selectable _button;

        private Tween _pressTween;
        private Sequence _releaseSequence;
        
        private Vector3 _startScale;
        private Vector3 _finishScale;
        private bool _isPressed;
        
        private Lazy<CanvasGroup[]> _canvasGroups;
        
        private void Awake()
        {
            if (_animationRoot == null)
            {
                _animationRoot = (RectTransform) transform;
            }
            
            _startScale = _animationRoot.localScale != Vector3.zero ? _animationRoot.localScale : Vector3.one;
            _finishScale = _startScale * .88f;
            _button = GetComponent<Selectable>();
            _canvasGroups = new Lazy<CanvasGroup[]>(() => GetComponentsInParent<CanvasGroup>().Reverse().ToArray());
        }
        
        private void OnEnable()
        {
            _animationRoot.localScale = _startScale;
        }
        
        private void OnDisable()
        {
            KillActiveTweens();
            _animationRoot.localScale = _startScale;
            _isPressed = false;
        }
        
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !CanAnimate())
            {
                return;
            }
            
            PlayPressAnimation();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            TryBounceBack();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            TryBounceBack();
        }

        private bool CanAnimate()
        {
            return _button == null ||
                   (_button.interactable && _canvasGroups.Value.All(g => g.interactable));
        }
        
        private void PlayPressAnimation()
        {
            KillActiveTweens();
            _isPressed = true;
            _pressTween = _animationRoot.DOScale(_finishScale, _duration);
            _pressTween.SetUpdate(true);
        }

        private void TryBounceBack()
        {
            if (!_isPressed)
            {
                return;
            }

            _isPressed = false;
            UpscaleBounce();
        }

        private void UpscaleBounce()
        {
            KillActiveTweens();
            _releaseSequence = DOTween.Sequence();
            _releaseSequence.SetUpdate(true);
            _releaseSequence.Append(_animationRoot.DOScale(_startScale * 1.05f, _duration));
            _releaseSequence.Append(_animationRoot.DOScale(_startScale, _duration * 2 / 3f));
            _releaseSequence.Play();
        }

        private void KillActiveTweens()
        {
            _pressTween?.Kill();
            _releaseSequence?.Kill();
        }
    }
}
