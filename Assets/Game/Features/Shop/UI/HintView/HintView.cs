using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Infrastructure
{
    public class HintView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField] private Animator _animator;
        [SerializeField] private float _defaultHideDelay = 2f;
        [SerializeField] private float _abortShowDelay = 0.2f;

        public float HideDelay => _defaultHideDelay;

        public float AbortShowDelay => _abortShowDelay;
        
        public event Action<HintView> OnHintHided;

        public void SetText(string text)
        {
            _text.text = text;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_text.rectTransform);
        }

        public async UniTask AnimationIn()
        {
            const string trigger = "Appear";

            _animator.speed = 1;
            _animator.SetTrigger(trigger);
            await _animator.WaitForStateComplete(trigger);
        }

        public async UniTask AnimationOut(float delay = 0f)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay));
            
            const string trigger = "Disappear";
            
            _animator.speed = 1;
            _animator.SetTrigger(trigger);

            await _animator.WaitForStateComplete(trigger);

            OnHintHided?.Invoke(this);
        }
    }
}
