using Book.Sell.API;
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

        //TODO should rename to OnPassivePurchase ? 
        void OnPassiveSale(Customer customer, PassiveSaleEvent saleEvent);

        /// <summary>A passive purchase attempt ended without a sale.</summary>
        void OnPassivePurchaseFailed(Customer customer);

        /// <summary>The customer finished its visit having bought <paramref name="purchasedBookCount"/>
        /// books (active recommendations + passive sales, >= 1). For the HUD completion animation.</summary>
        void OnPurchaseCompleted(Customer customer, int purchasedBookCount);

        /// <summary>A customer acquired the interaction lock and the active minigame opens for them.</summary>
        void OnActiveRequestStarted(Customer customer, RequestConfig request);

        /// <summary>The customer starts leaving — its thought bubble should be cleared so it walks away
        /// without any HUD. Feedback (Failed / bought / completed) has already had its on-screen dwell
        /// in the preceding steps.</summary>
        void OnHideThoughtBubble(Customer customer);
    }
}
