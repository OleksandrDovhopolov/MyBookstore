namespace Game.Configs.Models
{
    [ConfigFile("days")]
    public sealed class DayConfig : IConfig
    {
        public string Id { get; set; }
        public int DayIndex { get; set; }

        /// <summary>
        /// Regular customers scheduled for this day. Consumed by the customer traffic resolver
        /// (see docs/INPROGRESS/CUSTOMER_TRAFFIC_COUNT_SYSTEM.md). Null = use the global default from
        /// <c>SalesTrafficSettings.DefaultCustomerCount</c>.
        /// </summary>
        public int? CustomerCount { get; set; }

        /// <summary>
        /// Whether location/decor/etc. traffic modifiers apply to <see cref="CustomerCount"/> for this day.
        /// Null (absent) = modifiers on. <c>false</c> is a hard override: the count is exact — no modifiers
        /// and no min/max clamp (e.g. scripted day 1 = exactly 3).
        /// </summary>
        public bool? ApplyModifiers { get; set; }
    }
}
