using System;

namespace Book.Sell.UI.Customer
{
    public interface ICustomerVisualRegistry
    {
        CustomerVisual GetById(string customerId);

        event Action<CustomerVisual> CustomerVisualSpawned;
        event Action<CustomerVisual> CustomerVisualDespawned;
    }
}
