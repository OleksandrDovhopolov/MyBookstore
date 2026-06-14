using System;
using System.Collections.Generic;

namespace Game.UI
{
    public sealed class LockMonitor
    {
        private readonly HashSet<Lock> _active = new();

        public event Action StateChanged;

        public bool HasAnyLock => _active.Count > 0;

        public Lock Acquire(object owner)
        {
            var l = new Lock(this, owner);
            _active.Add(l);
            StateChanged?.Invoke();
            return l;
        }

        internal void Release(Lock l)
        {
            if (_active.Remove(l))
            {
                StateChanged?.Invoke();
            }
        }
    }
}
