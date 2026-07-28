using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI;
using Infrastructure;
using UIShared;
using UnityEngine;

namespace Game.Shop
{
    public class InfoWidgetView : WindowView
    {
        private const float HintHideDelay = 2f;
        
        [SerializeField] private UIListPool<HintView> _hintPool;

        public event Action OnComplete;

        private readonly List<HintView> _hints = new();
        private readonly List<UniTask> _animations = new();
        private CancellationTokenSource _cancellationTokenSource;

        public void ShowHint(string text)
        {
            HideCurrentHints();

            var hintView = _hintPool.GetNext();
            _hints.Add(hintView);
            
            hintView.SetText(text);
            hintView.OnHintHided += OnHintHidedHandler;
            
            _cancellationTokenSource = new CancellationTokenSource();
            _animations.Add(PlayHintAnimation(hintView, _cancellationTokenSource.Token));
        }
        
        private void HideCurrentHints()
        {
            _cancellationTokenSource?.Cancel();
            _animations.Clear();
            
            foreach (var hint in _hints)
                hint.AnimationOut(hint.AbortShowDelay).Forget();
            
            _hints.Clear();
        }
        
        private async UniTask PlayHintAnimation(HintView hintView, CancellationToken cancellationToken)
        {
            await hintView.AnimationIn();
            await UniTask.Delay(TimeSpan.FromSeconds(hintView.HideDelay), cancellationToken: cancellationToken);
            _hints.Remove(hintView);
            await hintView.AnimationOut();
        }
        
        private void OnHintHidedHandler(HintView hintView)
        {
            hintView.OnHintHided -= OnHintHidedHandler;
            _hints.Remove(hintView);

            if (_hints.Count != 0) return;
            
            _animations.Clear();
            _hintPool.DisableAll();
            OnComplete?.Invoke();
        }
    }
}
