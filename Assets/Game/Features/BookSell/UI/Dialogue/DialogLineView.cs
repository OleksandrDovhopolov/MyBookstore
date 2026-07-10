using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Book.Sell.UI
{
    /// <summary>
    /// One reply in the dialogue feed (GAME-6): a speaker name + its text in a bubble, aligned to a side
    /// (messenger style) and revealed with a DOTween fade+scale appear followed by a letter-by-letter
    /// typewriter. Pooled via <see cref="UIShared.UIListPool{T}"/> — hence <see cref="ICleanup"/> so a reused
    /// instance never shows stale text or a stuck tween. Dumb view: it only renders/animates itself; the flow
    /// (which reply is next, side assignment, blocking clicks) lives in <see cref="DialogWindow"/>.
    ///
    /// The new bubble/layout fields are optional: if unassigned, the view degrades to plain text + typewriter
    /// (no side alignment, no appear animation) instead of throwing — useful while the prefab is being built.
    /// </summary>
    public sealed class DialogLineView : MonoBehaviour, ICleanup
    {
        [Header("Content")]
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _textLabel;

        [Header("Bubble / layout")]
        [Tooltip("Row root layout — its childAlignment flips the bubble left/right.")]
        [SerializeField] private HorizontalLayoutGroup _rowLayout;
        [SerializeField] private CanvasGroup _bubbleCanvasGroup;   // fade
        [SerializeField] private RectTransform _bubble;            // scale
        [SerializeField] private LayoutElement _bubbleLayout;      // width cap
        [Tooltip("Max bubble width in px (~70% of the panel). Long replies wrap at this width.")]
        [SerializeField] private float _maxBubbleWidth = 600f;

        [Header("Appear animation")]
        [SerializeField] private float _appearDuration = 0.2f;
        [SerializeField] private float _appearScaleFrom = 0.85f;

        [Header("Typewriter")]
        [Tooltip("Typewriter speed in characters per second.")]
        [SerializeField] private float _charsPerSecond = 40f;

        private Sequence _appearTween;

        /// <summary>Aligns the bubble to the left or right of its row.</summary>
        public void SetSide(bool isRight)
        {
            if (_rowLayout != null)
                _rowLayout.childAlignment = isRight ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
        }

        /// <summary>Sets the speaker + full text (hidden until <see cref="RevealAsync"/>) and caps the bubble
        /// width so short replies stay compact and long ones wrap at <see cref="_maxBubbleWidth"/>.</summary>
        public void Bind(string speaker, string text)
        {
            if (_nameLabel != null) _nameLabel.text = speaker ?? string.Empty;

            if (_textLabel != null)
            {
                _textLabel.text = text ?? string.Empty;
                _textLabel.maxVisibleCharacters = 0;

                // LayoutElement has no max-width, so cap the preferred width ourselves: compact when short,
                // wraps at _maxBubbleWidth when long.
                if (_bubbleLayout != null)
                {
                    var preferred = _textLabel.GetPreferredValues(_textLabel.text).x;
                    _bubbleLayout.preferredWidth = Mathf.Min(preferred, _maxBubbleWidth);
                }
            }
        }

        /// <summary>
        /// Appear (DOTween fade+scale), then type the text out. Cancellation (window closed mid-reveal) is a
        /// normal exit — swallowed, not rethrown.
        /// </summary>
        public async UniTask RevealAsync(CancellationToken ct)
        {
            try
            {
                await PlayAppearAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_textLabel == null) return;

            // ForceMeshUpdate first: right after assigning .text the textInfo is stale and characterCount can
            // read 0, which would make the reveal finish instantly.
            _textLabel.ForceMeshUpdate();
            var total = _textLabel.textInfo.characterCount;

            if (total <= 0 || _charsPerSecond <= 0f)
            {
                _textLabel.maxVisibleCharacters = int.MaxValue;
                return;
            }

            var stepSeconds = 1f / _charsPerSecond;
            try
            {
                for (var shown = 1; shown <= total; shown++)
                {
                    _textLabel.maxVisibleCharacters = shown;
                    if (shown < total)
                        await UniTask.Delay(TimeSpan.FromSeconds(stepSeconds), cancellationToken: ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Window closed while typing — stop quietly.
            }
        }

        // Fade 0→1 + scale _appearScaleFrom→1 via DOTween.To (core, no DOTween.Modules extension methods).
        private async UniTask PlayAppearAsync(CancellationToken ct)
        {
            if (_bubbleCanvasGroup == null || _bubble == null)
                return;   // no bubble refs — degrade to plain (no appear anim)

            KillTween();
            _bubbleCanvasGroup.alpha = 0f;
            _bubble.localScale = Vector3.one * _appearScaleFrom;

            _appearTween = DOTween.Sequence()
                .SetUpdate(true)   // unscaled time, like the rest of the UI
                .Join(DOTween.To(() => _bubbleCanvasGroup.alpha, a => _bubbleCanvasGroup.alpha = a, 1f, _appearDuration))
                .Join(DOTween.To(() => _bubble.localScale, s => _bubble.localScale = s, Vector3.one, _appearDuration)
                    .SetEase(Ease.OutBack));

            await AwaitSequenceAsync(_appearTween, ct);
        }

        // Awaits a DOTween sequence via UniTaskCompletionSource + ct.Register (mirrors ResourceAnimationService)
        // — non-polling, distinguishes complete / kill / cancel. Cancellation kills the tween and throws OCE.
        private static async UniTask AwaitSequenceAsync(Sequence sequence, CancellationToken ct)
        {
            if (sequence == null) return;

            var completion = new UniTaskCompletionSource();

            using var registration = ct.Register(() =>
            {
                if (sequence.IsActive()) sequence.Kill(false);
                completion.TrySetCanceled(ct);
            });

            sequence.OnComplete(() => completion.TrySetResult());
            sequence.OnKill(() =>
            {
                if (!ct.IsCancellationRequested)
                    completion.TrySetResult();
            });

            sequence.Play();
            await completion.Task;
        }

        private void KillTween()
        {
            if (_appearTween != null && _appearTween.IsActive())
                _appearTween.Kill();
            _appearTween = null;
        }

        public void Cleanup()
        {
            KillTween();

            if (_nameLabel != null) _nameLabel.text = string.Empty;
            if (_textLabel != null)
            {
                _textLabel.text = string.Empty;
                _textLabel.maxVisibleCharacters = int.MaxValue;
            }

            if (_bubbleCanvasGroup != null) _bubbleCanvasGroup.alpha = 1f;
            if (_bubble != null) _bubble.localScale = Vector3.one;
        }
    }
}
