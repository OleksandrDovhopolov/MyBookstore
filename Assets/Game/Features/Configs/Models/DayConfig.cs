namespace Game.Configs.Models
{
    [ConfigFile("days")]
    public sealed class DayConfig : IConfig
    {
        public string Id { get; set; }
        public int DayIndex { get; set; }
        public string TitleKey { get; set; }

        /// <summary>
        /// Regular customers scheduled for this day. Consumed by the customer traffic resolver
        /// (see docs/INPROGRESS/CUSTOMER_TRAFFIC_COUNT_SYSTEM.md). Null = use the global default from
        /// <c>SalesTrafficSettings.DefaultCustomerCount</c>.
        /// </summary>
        public int? CustomerCount { get; set; }

        /// <summary>
        /// How many of this day's customers arrive with an active (scripted) request. Consumed by the
        /// active-request count resolver. Null = use the global default from
        /// <c>SalesTrafficSettings.DefaultActiveRequestCount</c>. Never raises the customer count: the
        /// spawner clamps it to the resolved customer count and to the request catalog size.
        /// </summary>
        public int? ActiveRequestCount { get; set; }

        /// <summary>
        /// Optional spawn waves for this day. Null/empty = one wave containing all customers.
        /// </summary>
        public int[] WaveSizes { get; set; }

        /// <summary>
        /// Delay between waves in seconds. Null = no delay.
        /// </summary>
        public float? WaveGapSeconds { get; set; }

        /// <summary>
        /// Whether location/decor/etc. traffic modifiers apply to <see cref="CustomerCount"/> and
        /// <see cref="ActiveRequestCount"/> for this day. Null (absent) = modifiers on. <c>false</c> is a
        /// hard override: both counts are exact — no modifiers and no min/max clamp (e.g. scripted day 1).
        /// </summary>
        public bool? ApplyModifiers { get; set; }
    }
}
