using System;

namespace Game.UI
{
    public sealed class Lock : IDisposable
    {
        private readonly LockMonitor _monitor;
        private bool _disposed;

        public object Owner { get; }

        internal Lock(LockMonitor monitor, object owner)
        {
            _monitor = monitor;
            Owner = owner;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _monitor.Release(this);
        }
    }
}
