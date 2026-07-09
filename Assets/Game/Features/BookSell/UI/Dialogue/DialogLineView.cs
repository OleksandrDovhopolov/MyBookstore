using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UIShared;
using UnityEngine;

namespace Book.Sell.UI
{
    /// <summary>
    /// One reply in the dialogue feed (GAME-6): a speaker name + its text, revealed letter-by-letter
    /// (typewriter). Pooled via <see cref="UIShared.UIListPool{T}"/> — hence <see cref="ICleanup"/> so a
    /// reused instance never shows stale text. Dumb view: it only renders and animates its own text; the
    /// flow (which reply is next, blocking clicks) lives in <see cref="DialogWindow"/>.
    /// </summary>
    public sealed class DialogLineView : MonoBehaviour, ICleanup
    {
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _textLabel;
        [Tooltip("Typewriter speed in characters per second.")]
        [SerializeField] private float _charsPerSecond = 40f;

        /// <summary>Sets the speaker + full text but hides all characters until <see cref="RevealAsync"/>.</summary>
        public void Bind(string speaker, string text)
        {
            if (_nameLabel != null) _nameLabel.text = speaker ?? string.Empty;
            if (_textLabel != null)
            {
                _textLabel.text = text ?? string.Empty;
                _textLabel.maxVisibleCharacters = 0;
            }
        }

        /// <summary>
        /// Types the text out by raising <c>maxVisibleCharacters</c> to the glyph count over time.
        /// Cancellation (window closed mid-reveal) is a normal exit — swallowed, not rethrown.
        /// </summary>
        public async UniTask RevealAsync(CancellationToken ct)
        {
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

        public void Cleanup()
        {
            if (_nameLabel != null) _nameLabel.text = string.Empty;
            if (_textLabel != null)
            {
                _textLabel.text = string.Empty;
                _textLabel.maxVisibleCharacters = int.MaxValue;
            }
        }
    }
}
