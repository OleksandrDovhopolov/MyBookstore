using Book.Sell.API;
using Book.Sell.Domain;

namespace Book.Sell.Services.Director
{
    public interface IPassiveSaleRule
    {
        void OnPassiveSale(Customer customer, PassiveSaleEvent sale, CustomerContext ctx);
    }
}
