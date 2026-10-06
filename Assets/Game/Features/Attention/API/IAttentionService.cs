using System;
using System.Collections.Generic;

namespace Game.Attention.API
{
    /// <summary>
    /// Central "there is something new here" state, shared by every feature that shows an unseen
    /// badge (Journal tabs, the HUD shop button, …). Keys are plain strings owned by the features
    /// themselves — this assembly deliberately knows none of them.
    /// <para>
    /// Unseen is always <b>derived</b>, never stored as a flag: a key is unseen when its source
    /// currently yields an id that is not in the persisted seen-set. <see cref="Changed"/> is
    /// edge-triggered — it fires only when some key's boolean actually flips.
    /// </para>
    /// </summary>
    public interface IAttentionService
    {
        bool HasUnseen(string key);

        /// <summary>
        /// True when any of <paramref name="keys"/> is unseen. Takes a list rather than
        /// <c>params</c> so badge refreshes (which run on every <see cref="Changed"/>) do not
        /// allocate — callers hold a static readonly array of their own keys.
        /// </summary>
        bool HasAnyUnseen(IReadOnlyList<string> keys);

        /// <summary>Marks everything the key currently yields as seen.</summary>
        void MarkSeen(string key);

        event Action Changed;
    }
}
