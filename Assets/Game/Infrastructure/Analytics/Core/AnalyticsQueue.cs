using System.Collections.Generic;
using UnityEngine;

namespace Analytics
{
    public sealed class AnalyticsQueue : IAnalyticsQueue
    {
        private const string LogPrefix = "[AnalyticsQueue]";

        private readonly Queue<IAnalyticsEvent> _queue = new();
        private readonly IAnalyticsConfig _config;
        private bool _overflowLogged;

        public AnalyticsQueue(IAnalyticsConfig config)
        {
            _config = config;
        }

        public int Count => _queue.Count;

        public void Enqueue(IAnalyticsEvent analyticsEvent)
        {
            if (analyticsEvent == null)
            {
                return;
            }

            var maxQueueSize = Mathf.Max(0, _config.MaxQueueSize);
            if (maxQueueSize == 0)
            {
                LogOverflowOnce();
                return;
            }

            while (_queue.Count >= maxQueueSize)
            {
                _queue.Dequeue();
                LogOverflowOnce();
            }

            _queue.Enqueue(analyticsEvent);
        }

        public bool TryDequeue(out IAnalyticsEvent analyticsEvent)
        {
            if (_queue.Count == 0)
            {
                analyticsEvent = null;
                return false;
            }

            analyticsEvent = _queue.Dequeue();
            return true;
        }

        public void Clear()
        {
            _queue.Clear();
            _overflowLogged = false;
        }

        private void LogOverflowOnce()
        {
            if (_overflowLogged)
            {
                return;
            }

            _overflowLogged = true;
            Debug.LogWarning($"{LogPrefix} Queue overflow. Old analytics events are being dropped.");
        }
    }
}
