using System;
using System.Collections.Generic;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Services;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap.Analytics
{
    public sealed class SalesDayAnalyticsListener : IStartable, IDisposable
    {
        private readonly ISalesDayController _sales;
        private readonly global::Analytics.IAnalyticsService _analytics;
        private int _passiveMisses;

        public SalesDayAnalyticsListener(ISalesDayController sales, global::Analytics.IAnalyticsService analytics)
        {
            _sales = sales ?? throw new ArgumentNullException(nameof(sales));
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
        }

        public void Start()
        {
            _sales.DayStarted += OnDayStarted;
            _sales.RecommendationResolved += OnRecommendationResolved;
            _sales.CustomerPassivePurchaseFailed += OnCustomerPassivePurchaseFailed;
            _sales.DayCompleted += OnDayCompleted;
        }

        public void Dispose()
        {
            _sales.DayStarted -= OnDayStarted;
            _sales.RecommendationResolved -= OnRecommendationResolved;
            _sales.CustomerPassivePurchaseFailed -= OnCustomerPassivePurchaseFailed;
            _sales.DayCompleted -= OnDayCompleted;
        }

        private void OnDayStarted(int day, string locationId)
        {
            _passiveMisses = 0;

            var parameters = new Dictionary<string, object>
            {
                [global::Analytics.AnalyticsParameterNames.Day] = day
            };
            AnalyticsParameterBag.AddString(parameters, global::Analytics.AnalyticsParameterNames.LocationId, locationId);

            _analytics.TrackEvent(new global::Analytics.AnalyticsEvent(
                global::Analytics.AnalyticsEventNames.DayStarted,
                parameters));
        }

        private void OnRecommendationResolved(RecommendationResult result)
        {
            if (result == null) return;

            var parameters = new Dictionary<string, object>
            {
                [global::Analytics.AnalyticsParameterNames.Tier] = (int)result.Tier,
                [global::Analytics.AnalyticsParameterNames.GoldEarned] = result.GoldEarned
            };
            AnalyticsParameterBag.AddString(parameters, global::Analytics.AnalyticsParameterNames.RequestId, result.RequestId);
            AnalyticsParameterBag.AddString(parameters, global::Analytics.AnalyticsParameterNames.BookId, result.BookId);

            _analytics.TrackEvent(new global::Analytics.AnalyticsEvent(
                global::Analytics.AnalyticsEventNames.ActiveSaleCompleted,
                parameters));
        }

        private void OnCustomerPassivePurchaseFailed(Customer customer, string genre)
        {
            _passiveMisses++;
        }

        private void OnDayCompleted(SalesDayResult result)
        {
            try
            {
                if (result == null) return;

                var parameters = new Dictionary<string, object>
                {
                    [global::Analytics.AnalyticsParameterNames.Day] = result.Day,
                    [global::Analytics.AnalyticsParameterNames.GoldEarned] = result.GoldEarned,
                    [global::Analytics.AnalyticsParameterNames.SalesCount] = result.SalesCount,
                    [global::Analytics.AnalyticsParameterNames.CustomersServed] = result.CustomersServed,
                    [global::Analytics.AnalyticsParameterNames.ExcellentCount] = result.ExcellentCount,
                    [global::Analytics.AnalyticsParameterNames.FailedCount] = result.FailedCount,
                    [global::Analytics.AnalyticsParameterNames.SkippedCount] = result.SkippedCount,
                    [global::Analytics.AnalyticsParameterNames.PassiveSalesCount] = result.PassiveSales?.Count ?? 0,
                    [global::Analytics.AnalyticsParameterNames.PassiveMisses] = _passiveMisses
                };
                AnalyticsParameterBag.AddString(parameters, global::Analytics.AnalyticsParameterNames.LocationId, result.LocationId);

                _analytics.TrackEvent(new global::Analytics.AnalyticsEvent(
                    global::Analytics.AnalyticsEventNames.DayCompleted,
                    parameters));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Analytics][SalesDay] DayCompleted listener failed: {exception}");
            }
        }
    }
}
