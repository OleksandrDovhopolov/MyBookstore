using Book.Sell.Domain;
using UnityEngine;

namespace Book.Sell.Services
{
    /// <summary>
    /// Designer-editable asset for global customer-traffic knobs (fallback default, min/max clamp,
    /// rounding). Produces a pure-domain <see cref="SalesTrafficSettings"/> at install time, mirroring
    /// <see cref="SalesTuningConfig"/>. Per-day counts live in days.json (DayConfig), not here.
    /// Assign on <c>LocationInstaller</c>; if unassigned the binding falls back to code defaults.
    /// </summary>
    [CreateAssetMenu(menuName = "Book Sell/Sales Traffic", fileName = "SalesTraffic")]
    public sealed class SalesTrafficConfig : ScriptableObject
    {
        [Header("Baseline")]
        [Tooltip("Кол-во покупателей для дня, у которого нет своего customerCount в days.json.")]
        [SerializeField] private int _defaultCustomerCount = 5;

        [Header("Clamp (non-hard-override days)")]
        [SerializeField] private int _minCustomerCount = 0;
        [SerializeField] private int _maxCustomerCount = 50;

        [Header("Active requests")]
        [Tooltip("Сколько покупателей приходят с активным запросом в день, у которого нет своего " +
                 "activeRequestCount в days.json. Размер каталога requests.json на это НЕ влияет — " +
                 "он лишь пул, из которого делается выборка.")]
        [SerializeField] private int _defaultActiveRequestCount = 1;

        [SerializeField] private int _minActiveRequestCount = 0;
        [SerializeField] private int _maxActiveRequestCount = 50;

        [Header("Rounding")]
        [SerializeField] private TrafficRounding _rounding = TrafficRounding.NearestAwayFromZero;

        public SalesTrafficSettings BuildSettings() => new()
        {
            DefaultCustomerCount = _defaultCustomerCount,
            MinCustomerCount = _minCustomerCount,
            MaxCustomerCount = _maxCustomerCount,
            DefaultActiveRequestCount = _defaultActiveRequestCount,
            MinActiveRequestCount = _minActiveRequestCount,
            MaxActiveRequestCount = _maxActiveRequestCount,
            Rounding = _rounding
        };
    }
}
