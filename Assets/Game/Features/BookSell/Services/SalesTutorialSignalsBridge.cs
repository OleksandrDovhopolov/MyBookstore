using System;
using System.Linq;
using Book.Sell.API;
using Book.Sell.Domain;
using Game.UI;
using MessagePipe;
using VContainer.Unity;

namespace Book.Sell.Services
{
    public sealed class SalesTutorialSignalsBridge : IStartable, IDisposable
    {
        private readonly ISalesDayController _sales;
        private readonly IPublisher<SalesCustomerPhaseChanged> _phasePublisher;
        private readonly IPublisher<SalesPassiveSaleHappened> _salePublisher;
        private readonly IPublisher<SalesPassivePurchaseFailed> _failPublisher;

        public SalesTutorialSignalsBridge(
            ISalesDayController sales,
            IPublisher<SalesCustomerPhaseChanged> phasePublisher,
            IPublisher<SalesPassiveSaleHappened> salePublisher,
            IPublisher<SalesPassivePurchaseFailed> failPublisher)
        {
            _sales = sales;
            _phasePublisher = phasePublisher;
            _salePublisher = salePublisher;
            _failPublisher = failPublisher;
        }

        public void Start()
        {
            _sales.CustomerPhaseChanged += OnCustomerPhaseChanged;
            _sales.CustomerPassiveSaleHappened += OnCustomerPassiveSaleHappened;
            _sales.CustomerPassivePurchaseFailed += OnCustomerPassivePurchaseFailed;
        }

        public void Dispose()
        {
            _sales.CustomerPhaseChanged -= OnCustomerPhaseChanged;
            _sales.CustomerPassiveSaleHappened -= OnCustomerPassiveSaleHappened;
            _sales.CustomerPassivePurchaseFailed -= OnCustomerPassivePurchaseFailed;
        }

        private void OnCustomerPhaseChanged(Customer customer)
        {
            _phasePublisher.Publish(new SalesCustomerPhaseChanged(
                customer?.Id,
                customer?.CharacterId,
                customer?.Phase.ToString()));
        }

        private void OnCustomerPassiveSaleHappened(Customer customer, PassiveSaleEvent sale)
        {
            _salePublisher.Publish(new SalesPassiveSaleHappened(
                customer?.Id,
                customer?.CharacterId,
                sale?.ResolvedGenre ?? sale?.MatchedGenres?.FirstOrDefault(),
                sale?.BookId));
        }

        private void OnCustomerPassivePurchaseFailed(Customer customer, string genre)
        {
            _failPublisher.Publish(new SalesPassivePurchaseFailed(
                customer?.Id,
                customer?.CharacterId,
                genre));
        }
    }
}
