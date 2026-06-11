using Game.Configs.Models;

namespace Book.Sell.Domain
{
    /// <summary>
    /// Facts the customer steps report up to the day controller. The controller re-emits them as
    /// public events for the View. Active-request resolution is driven by the controller itself
    /// (player input), so it is not part of this sink.
    /// </summary>
    public interface ISalesDaySink
    {
        void OnPhaseChanged(Customer customer, CustomerPhase phase);

        /// <summary>A customer targeted a book and reserved it (soft-lock) before committing the sale.</summary>
        void OnBookReserved(Customer customer, string bookId);

        /// <summary>A reservation was released without a sale (customer aborted mid-purchase).</summary>
        void OnBookReleased(Customer customer, string bookId);

        void OnPassiveSale(Customer customer, PassiveSaleEvent saleEvent);

        /// <summary>A customer acquired the interaction lock and the active minigame opens for them.</summary>
        void OnActiveRequestStarted(Customer customer, RequestConfig request);
    }
}
