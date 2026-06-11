using Book.Sell.Domain;

namespace Book.Sell.Services
{
    /// <summary>
    /// Source of the daily setup (location + shelf). Until the Preparation phase exists,
    /// the default implementation falls back to the first location + first N books from BookConfig.
    /// </summary>
    public interface ISalesSetupProvider
    {
        SalesSessionSetup BuildForDay(int day);
    }
}
