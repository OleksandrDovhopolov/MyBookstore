using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Analytics
{
    public sealed class CompositeAnalyticsService : IAnalyticsService
    {
        private const string LogPrefix = "[Analytics]";

        private readonly IAnalyticsConfig _config;
        private readonly IAnalyticsContextProvider _contextProvider;
        private readonly IAnalyticsEventValidator _validator;
        private readonly IAnalyticsRouter _router;
        private readonly IAnalyticsEventMapper _mapper;
        private readonly IAnalyticsQueue _queue;
        private readonly IAnalyticsConsentService _consentService;
        private readonly IReadOnlyList<IAnalyticsProvider> _providers;
        private readonly IAnalyticsUserContext _userContext;
        private string _userId;

        public CompositeAnalyticsService(
            IAnalyticsConfig config,
            IAnalyticsContextProvider contextProvider,
            IAnalyticsEventValidator validator,
            IAnalyticsRouter router,
            IAnalyticsEventMapper mapper,
            IAnalyticsQueue queue,
            IAnalyticsConsentService consentService,
            IEnumerable<IAnalyticsProvider> providers)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _consentService = consentService ?? throw new ArgumentNullException(nameof(consentService));
            _providers = providers != null
                ? providers.ToList()
                : Array.Empty<IAnalyticsProvider>();
            _userContext = contextProvider as IAnalyticsUserContext;
        }

        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            foreach (var provider in _providers)
            {
                if (!provider.IsEnabled)
                {
                    continue;
                }

                try
                {
                    provider.Initialize();
                    if (!string.IsNullOrWhiteSpace(_userId))
                    {
                        provider.SetUserId(_userId);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogError($"{LogPrefix} Provider '{provider.ProviderId}' initialization failed: {exception}");
                }
            }

            IsInitialized = true;
            Flush();
        }

        public void TrackEvent(IAnalyticsEvent analyticsEvent)
        {
            if (!TrackEventInternal(analyticsEvent))
            {
                _queue.Enqueue(analyticsEvent);
            }
        }

        public void SetUserId(string userId)
        {
            _userId = string.IsNullOrWhiteSpace(userId) ? null : userId;
            _userContext?.SetUserId(_userId);

            foreach (var provider in _providers)
            {
                if (!provider.IsEnabled)
                {
                    continue;
                }

                try
                {
                    provider.SetUserId(_userId);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"{LogPrefix} Provider '{provider.ProviderId}' SetUserId failed: {exception}");
                }
            }

            Flush();
        }

        public void SetUserProperty(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogWarning($"{LogPrefix} User property key is empty.");
                return;
            }

            foreach (var provider in _providers)
            {
                if (!provider.IsEnabled)
                {
                    continue;
                }

                try
                {
                    provider.SetUserProperty(key, value);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"{LogPrefix} Provider '{provider.ProviderId}' SetUserProperty failed: {exception}");
                }
            }
        }

        public void Flush()
        {
            var count = _queue.Count;
            for (var i = 0; i < count; i++)
            {
                if (!_queue.TryDequeue(out var analyticsEvent))
                {
                    break;
                }

                if (!TrackEventInternal(analyticsEvent))
                {
                    _queue.Enqueue(analyticsEvent);
                }
            }

            foreach (var provider in _providers)
            {
                if (!provider.IsEnabled)
                {
                    continue;
                }

                try
                {
                    provider.Flush();
                }
                catch (Exception exception)
                {
                    Debug.LogError($"{LogPrefix} Provider '{provider.ProviderId}' Flush failed: {exception}");
                }
            }
        }

        private bool TrackEventInternal(IAnalyticsEvent analyticsEvent)
        {
            if (!_config.IsAnalyticsEnabled || !_consentService.CanSendAnalytics)
            {
                return true;
            }

            if (analyticsEvent == null)
            {
                Debug.LogWarning($"{LogPrefix} Null event skipped.");
                return true;
            }

            if (!IsInitialized || !HasUserIdForSending())
            {
                return false;
            }

            var enabledProviders = _providers
                .Where(provider => provider.IsEnabled)
                .ToArray();

            if (enabledProviders.Length == 0)
            {
                Debug.LogWarning($"{LogPrefix} No enabled analytics providers.");
                return true;
            }

            var initializedProviders = enabledProviders
                .Where(provider => provider.IsInitialized)
                .ToArray();

            if (initializedProviders.Length == 0)
            {
                return false;
            }

            var mergedEvent = MergeWithCommonParameters(analyticsEvent);
            if (!_validator.Validate(mergedEvent, out var error))
            {
                Debug.LogWarning($"{LogPrefix} Event '{analyticsEvent.Name}' skipped: {error}");
                return true;
            }

            var matchedAnyProvider = false;
            foreach (var provider in initializedProviders)
            {
                if (!_router.ShouldSendToProvider(mergedEvent, provider))
                {
                    continue;
                }

                matchedAnyProvider = true;
                try
                {
                    var mappedEvent = _mapper.Map(mergedEvent, provider.ProviderId);
                    provider.TrackEvent(mappedEvent);

                    if (_config.IsDebugLoggingEnabled)
                    {
                        Debug.Log($"{LogPrefix} Sent '{mappedEvent.Name}' to '{provider.ProviderId}'.");
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogError($"{LogPrefix} Provider '{provider.ProviderId}' TrackEvent failed: {exception}");
                }
            }

            if (!matchedAnyProvider && _config.IsDebugLoggingEnabled)
            {
                Debug.Log($"{LogPrefix} Event '{mergedEvent.Name}' was not routed to any provider.");
            }

            return true;
        }

        private IAnalyticsEvent MergeWithCommonParameters(IAnalyticsEvent analyticsEvent)
        {
            var parameters = new Dictionary<string, object>();
            var commonParameters = _contextProvider.GetCommonParameters();
            if (commonParameters != null)
            {
                foreach (var parameter in commonParameters)
                {
                    if (parameter.Value != null)
                    {
                        parameters[parameter.Key] = parameter.Value;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(_userId))
            {
                parameters[AnalyticsParameterNames.UserId] = _userId;
            }

            if (analyticsEvent.Parameters != null)
            {
                foreach (var parameter in analyticsEvent.Parameters)
                {
                    parameters[parameter.Key] = parameter.Value;
                }
            }

            return new AnalyticsEvent(analyticsEvent.Name, parameters);
        }

        private bool HasUserIdForSending()
        {
            if (_config.SendEventsWithoutUserId || !string.IsNullOrWhiteSpace(_userId))
            {
                return true;
            }

            var commonParameters = _contextProvider.GetCommonParameters();
            return commonParameters != null &&
                   commonParameters.TryGetValue(AnalyticsParameterNames.UserId, out var userId) &&
                   userId is string userIdString &&
                   !string.IsNullOrWhiteSpace(userIdString);
        }
    }
}
