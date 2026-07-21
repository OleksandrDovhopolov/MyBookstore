namespace Book.Sell.Domain.Steps
{
    /// <summary>
    /// Marker for steps that belong to a customer's closing sequence (e.g. CompletePurchase, Leave) and
    /// must still run when the passive chain ends early. Used by <see cref="Book.Sell.Domain.CustomerPlan"/>
    /// to keep runtime insertions (<c>InsertNext</c>/<c>InsertBeforeClosing</c>) out of the closing tail.
    /// </summary>
    public interface IClosingStep : ICustomerStep
    {
    }
}
