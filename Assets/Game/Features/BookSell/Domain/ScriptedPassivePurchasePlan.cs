using System;
using System.Collections.Generic;

namespace Book.Sell.Domain
{
    /// <summary>
    /// Per-customer cursor over authored passive attempts.
    /// </summary>
    public sealed class ScriptedPassivePurchasePlan
    {
        private readonly IReadOnlyList<ScriptedPassiveAttempt> _attempts;
        private int _index;

        public ScriptedPassivePurchasePlan(IReadOnlyList<ScriptedPassiveAttempt> attempts)
        {
            _attempts = attempts ?? Array.Empty<ScriptedPassiveAttempt>();
        }

        public int Count => _attempts.Count;

        public bool TryConsumeNext(out ScriptedPassiveAttempt attempt)
        {
            attempt = null;
            while (_index < _attempts.Count)
            {
                attempt = _attempts[_index++];
                if (attempt != null)
                    return true;
            }

            return false;
        }
    }
}