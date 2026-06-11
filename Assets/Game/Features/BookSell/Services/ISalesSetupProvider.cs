using Book.Sell.Domain;

namespace Book.Sell.Services
{
    /// <summary>
    /// Откуда Sales берёт setup дня (локация + полка). До появления Подготовки —
    /// fallback на первую локацию + первые N книг из BookConfig.
    /// </summary>
    public interface ISalesSetupProvider
    {
        SalesSessionSetup BuildForDay(int day);
    }
}
