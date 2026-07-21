using Book.Sell.Domain;

namespace Book.Sell.Services
{
    /// <summary>
    /// Owns "how many customers arrive with an active request today" — the request counterpart of
    /// <see cref="ICustomerTrafficResolver"/>, with the same shape (day baseline → contributors →
    /// clamp → final) and the same deterministic contract (no <c>ISalesRandom</c>).
    ///
    /// Exists because the request CATALOG (requests.json) must not decide the size of a day: before this,
    /// the spawner floored traffic at "number of enabled requests", so 49 catalog entries forced 49
    /// customers. Demand now comes from the day config; the catalog is only a pool to draw from.
    /// </summary>
    public interface IActiveRequestCountResolver
    {
        ActiveRequestCountResult Resolve(SalesSessionSetup setup, SalesTuning tuning);
    }
}
