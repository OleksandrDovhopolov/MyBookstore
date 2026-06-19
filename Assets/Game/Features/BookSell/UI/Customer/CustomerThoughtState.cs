namespace Book.Sell.UI.Customer
{
    public enum CustomerThoughtState
    {
        None = 0,
        Thinking,           // "..." dots
        BookPicked,         // book icon (with scale-in animation)
        Comment,            // text comment
        ThinkingNext,       // "..." dots again (after first purchase)
        Rejected,           // crossed-out book + replacement
        PassiveSaleFailed,  // passive purchase attempt ended without a sale
    }
}
