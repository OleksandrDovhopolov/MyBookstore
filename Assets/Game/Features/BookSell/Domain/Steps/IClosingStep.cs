namespace Book.Sell.Domain.Steps
{
    /// <summary>
    /// Marker for steps that belong to a customer's closing sequence (e.g. CompletePurchase, Leave)
    /// and must still run when the purchase cycle is abandoned early. On a passive-failure abort the
    /// customer skips remaining purchase steps and resumes at the first <see cref="IClosingStep"/>.
    /// </summary>
    public interface IClosingStep : ICustomerStep
    {
    }
}
