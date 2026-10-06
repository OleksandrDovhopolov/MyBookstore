using System;
using System.Collections.Generic;

namespace Game.Attention.API
{
    /// <summary>
    /// Supplies the ids that currently exist for one attention key. The seen-set for those ids is
    /// owned and persisted by the attention service, so a source is stateless — it only reports what
    /// exists now and raises <see cref="Changed"/> when that set may have changed.
    /// </summary>
    public interface IAttentionSource
    {
        string Key { get; }

        IEnumerable<string> CurrentIds { get; }

        event Action Changed;
    }
}
