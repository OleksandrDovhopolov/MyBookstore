using System;
using Game.UI;

namespace Dialogue
{
    /// <summary>
    /// Carries the <see cref="DialoguePayload"/> into <see cref="DialogWindow"/>, plus an optional completion
    /// callback. The window is created by the bootstrap-scoped window factory, so any gameplay-scoped follow-up
    /// (e.g. releasing the sales interaction lock via the sales day controller) is passed in as a neutral
    /// <see cref="OnCompleted"/> delegate by the presenter that owns it — the dialogue module itself stays
    /// decoupled from the sales feature.
    ///
    /// <see cref="OnCompleted"/> may be null in debug/cheat mode (opened outside a sales scene): the window
    /// then just renders the graph and closes, with nothing to resolve.
    /// </summary>
    public sealed class DialogWindowArgs : WindowArgs
    {
        public DialoguePayload Payload { get; }
        public Action OnCompleted { get; }

        public DialogWindowArgs(DialoguePayload payload, Action onCompleted = null)
        {
            Payload = payload;
            OnCompleted = onCompleted;
        }
    }
}
