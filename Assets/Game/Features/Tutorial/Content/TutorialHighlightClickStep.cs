using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI;
using Game.Tutorial;
using Game.Tutorial.API;
using Game.Tutorial.Presentation;
using Infrastructure.TutorialUI;
using MessagePipe;
using UnityEngine;

namespace Game.Tutorial.Content
{
    public sealed class TutorialHighlightClickStep : ITutorialStep
    {
        private readonly TutorialOverlayController _overlay;
        private readonly ITutorialTargetRegistry _targets;
        private readonly string _targetId;
        private readonly Func<string> _textFactory;
        private readonly string _placement;
        private readonly bool _pointer;
        private readonly TutorialPointerPlacement _pointerPlacement;
        private readonly Func<bool> _gate;
        private readonly IPublisher<SalesPauseRequested> _pausePublisher;
        private readonly bool _pauseSales;
        private readonly Vector2 _pointerOffset;

        public TutorialHighlightClickStep(
            string id,
            TutorialOverlayController overlay,
            ITutorialTargetRegistry targets,
            string targetId,
            string text,
            string placement,
            bool pointer = true,
            Func<bool> gate = null,
            TutorialPointerPlacement pointerPlacement = TutorialPointerPlacement.Top,
            IPublisher<SalesPauseRequested> pausePublisher = null,
            bool pauseSales = false,
            Vector2 pointerOffset = default)
            : this(
                id,
                overlay,
                targets,
                targetId,
                () => text,
                placement,
                pointer,
                gate,
                pointerPlacement,
                pausePublisher,
                pauseSales,
                pointerOffset)
        {
        }

        public TutorialHighlightClickStep(
            string id,
            TutorialOverlayController overlay,
            ITutorialTargetRegistry targets,
            string targetId,
            Func<string> textFactory,
            string placement,
            bool pointer = true,
            Func<bool> gate = null,
            TutorialPointerPlacement pointerPlacement = TutorialPointerPlacement.Top,
            IPublisher<SalesPauseRequested> pausePublisher = null,
            bool pauseSales = false,
            Vector2 pointerOffset = default)
        {
            Id = id;
            _overlay = overlay;
            _targets = targets;
            _targetId = targetId;
            _textFactory = textFactory;
            _placement = placement;
            _pointer = pointer;
            _pointerPlacement = pointerPlacement;
            _gate = gate;
            _pausePublisher = pausePublisher;
            _pauseSales = pauseSales;
            _pointerOffset = pointerOffset;
        }

        public string Id { get; }
        public string TargetId => _targetId;
        public TutorialPointerPlacement PointerPlacement => _pointerPlacement;

        public async UniTask ExecuteAsync(CancellationToken ct)
        {
            if (_gate != null && !_gate())
                return;

            if (_targets == null || !_targets.TryGetTarget(_targetId, out var target))
            {
                Debug.LogWarning($"{TutorialLog.Prefix} highlight target '{_targetId}' not found; auto-advancing.");
                return;
            }

            try
            {
                SetSalesPaused(true);
                await _overlay.HighlightAndWaitClickAsync(
                    target,
                    _textFactory?.Invoke(),
                    _placement,
                    _pointer,
                    _pointerPlacement,
                    _pointerOffset,
                    ct);
            }
            finally
            {
                _overlay.HideHighlight();
                SetSalesPaused(false);
            }
        }

        private void SetSalesPaused(bool paused)
        {
            if (!_pauseSales)
                return;

            _pausePublisher?.Publish(new SalesPauseRequested(paused));
        }
    }
}
