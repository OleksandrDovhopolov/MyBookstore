using Book.Sell.Domain;

namespace Book.Sell.Services
{
    /// <summary>
    /// Owns "how many regular customers visit today". The production base spawner asks this, then builds
    /// that many regular customer plans — the resolver never builds customers. Deterministic from
    /// config/modifiers (no <c>ISalesRandom</c>). See docs/INPROGRESS/CUSTOMER_TRAFFIC_COUNT_SYSTEM.md.
    /// </summary>
    public interface ICustomerTrafficResolver
    {
        CustomerTrafficResult Resolve(SalesSessionSetup setup, SalesTuning tuning);
    }
}
