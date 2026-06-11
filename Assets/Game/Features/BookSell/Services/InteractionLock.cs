using System.Collections.Generic;

namespace Book.Sell.Services
{
    /// <inheritdoc cref="IInteractionLock"/>
    public sealed class InteractionLock : IInteractionLock
    {
        private readonly List<object> _waiters = new();
        private object _holder;

        public bool IsHeld => _holder != null;
        public object CurrentHolder => _holder;

        public bool TryAcquire(object token)
        {
            if (token == null) return false;

            if (_holder == token) return true;        // re-acquire by holder
            if (_holder != null)
            {
                if (!_waiters.Contains(token)) _waiters.Add(token);  // FIFO enqueue (idempotent)
                return false;
            }

            // Free: only the head of the queue (or a fresh token when queue is empty) may take it.
            if (_waiters.Count > 0 && !ReferenceEquals(_waiters[0], token))
            {
                if (!_waiters.Contains(token)) _waiters.Add(token);
                return false;
            }

            if (_waiters.Count > 0 && ReferenceEquals(_waiters[0], token))
                _waiters.RemoveAt(0);

            _holder = token;
            return true;
        }

        public void Release(object token)
        {
            if (_holder == token) _holder = null;
            else _waiters.Remove(token);  // a waiter that left before its turn
        }

        public bool IsNextOrHolding(object token)
        {
            if (_holder == token) return true;
            if (_holder != null) return false;
            return _waiters.Count == 0 || ReferenceEquals(_waiters[0], token);
        }
    }
}
