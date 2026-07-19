using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Tutorial.API;
using Game.Tutorial.Presentation;
using Infrastructure.TutorialUI;
using UnityEngine;

namespace Game.Tutorial.Content
{
    public sealed class TutorialHighlightClickStep : ITutorialStep
    {
        private const string LogPrefix = "[Tutorial]";

        private readonly TutorialOverlayController _overlay;
        private readonly ITutorialTargetRegistry _targets;
        private readonly string _targetId;
        private readonly Func<string> _textFactory;
        private readonly string _placement;
        private readonly bool _pointer;

        public TutorialHighlightClickStep(
            string id,
            TutorialOverlayController overlay,
            ITutorialTargetRegistry targets,
            string targetId,
            string text,
            string placement,
            bool pointer = true)
            : this(id, overlay, targets, targetId, () => text, placement, pointer)
        {
        }

        public TutorialHighlightClickStep(
            string id,
            TutorialOverlayController overlay,
            ITutorialTargetRegistry targets,
            string targetId,
            Func<string> textFactory,
            string placement,
            bool pointer = true)
        {
            Id = id;
            _overlay = overlay;
            _targets = targets;
            _targetId = targetId;
            _textFactory = textFactory;
            _placement = placement;
            _pointer = pointer;
        }

        public string Id { get; }

        public async UniTask ExecuteAsync(CancellationToken ct)
        {
            if (_targets == null || !_targets.TryGetTarget(_targetId, out var target))
            {
                Debug.LogWarning($"{LogPrefix} highlight target '{_targetId}' not found; auto-advancing.");
                return;
            }

            try
            {
                await _overlay.HighlightAndWaitClickAsync(
                    target,
                    _textFactory?.Invoke(),
                    _placement,
                    _pointer,
                    ct);
            }
            finally
            {
                _overlay.HideHighlight();
            }
        }
    }
}
