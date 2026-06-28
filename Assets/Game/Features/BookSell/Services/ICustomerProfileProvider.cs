using Book.Sell.Domain;

namespace Book.Sell.Services
{
    /// <summary>
    /// Policy that builds a customer's desire <see cref="CustomerProfile"/> at spawn time. Location
    /// demand is one source, not the truth — persona/quest providers can plug in later. Implementations
    /// must return at least one genre whenever any genre exists for the day.
    /// </summary>
    public interface ICustomerProfileProvider
    {
        CustomerProfile Create(SalesSessionSetup setup, ISalesRandom random);
    }
}
