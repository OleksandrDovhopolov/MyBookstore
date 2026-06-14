using System.Collections.Generic;
using UnityEngine;

namespace Analytics
{
    [CreateAssetMenu(menuName = "Game/Analytics/Analytics Config", fileName = "AnalyticsConfig")]
    public sealed class AnalyticsConfigSO : ScriptableObject, IAnalyticsConfig
    {
        [SerializeField] private bool _isAnalyticsEnabled = true;
        [SerializeField] private bool _isDebugLoggingEnabled = true;
        [SerializeField] private string _environment = "development";
        [SerializeField] private List<string> _enabledProviderIds = new()
        {
            AnalyticsProviderIds.Debug,
            AnalyticsProviderIds.Firebase
        };
        [SerializeField] private int _maxQueueSize = 100;
        [SerializeField] private int _maxEventNameLength = 40;
        [SerializeField] private int _maxParameterKeyLength = 40;
        [SerializeField] private int _maxParameterCount = 50;
        [SerializeField] private bool _sendEventsWithoutUserId = true;

        public bool IsAnalyticsEnabled => _isAnalyticsEnabled;

        public bool IsDebugLoggingEnabled => _isDebugLoggingEnabled;

        public string Environment => string.IsNullOrWhiteSpace(_environment) ? "development" : _environment;

        public IReadOnlyCollection<string> EnabledProviderIds => _enabledProviderIds;

        public int MaxQueueSize => Mathf.Max(0, _maxQueueSize);

        public int MaxEventNameLength => Mathf.Max(1, _maxEventNameLength);

        public int MaxParameterKeyLength => Mathf.Max(1, _maxParameterKeyLength);

        public int MaxParameterCount => Mathf.Max(0, _maxParameterCount);

        public bool SendEventsWithoutUserId => _sendEventsWithoutUserId;
    }
}
