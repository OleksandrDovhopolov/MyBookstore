using System;
using System.Collections.Generic;
using Book.Sell.API;
using Book.Sell.Domain;

namespace Book.Sell.Services.Director
{
    public sealed class CustomerDirector : ICustomerDirector
    {
        private readonly IReadOnlyList<IPassiveSaleRule> _passiveSaleRules;

        public CustomerDirector(IEnumerable<IPassiveSaleRule> passiveSaleRules)
        {
            if (passiveSaleRules == null)
            {
                _passiveSaleRules = Array.Empty<IPassiveSaleRule>();
                return;
            }

            _passiveSaleRules = new List<IPassiveSaleRule>(passiveSaleRules);
        }

        public void OnPassiveSale(Customer customer, PassiveSaleEvent sale, CustomerContext ctx)
        {
            for (var i = 0; i < _passiveSaleRules.Count; i++)
                _passiveSaleRules[i].OnPassiveSale(customer, sale, ctx);
        }
    }
}
