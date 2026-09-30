using System;
using System.Collections.Generic;
using Game.Attention.API;
using Game.DayCycle.Day;
using Game.LocationVisits.API;
using Game.Shop.API;

namespace Game.Bootstrap
{
    /// <summary>
    /// Reports which decor lots are currently on offer, so the HUD shop button can badge when new
    /// stock unlocks. Lives in the installer assembly (like <c>TutorialReevaluationBridge</c>) because
    /// it is cross-feature glue: <c>Game.Shop</c> references neither <c>DayCycle</c> nor
    /// <c>Game.LocationVisits.API</c>, and adding them there would break the layer rules in
    /// docs/ASMDEF_RULES.md.
    /// <para>
    /// Both change signals are needed and neither is redundant: <c>dayAtLeast</c> gates move on
    /// <see cref="IDayProgressService.PhaseChanged"/>, while <c>visitLocation</c> gates move on
    /// <see cref="ILocationVisitChangeSource.Changed"/>. Subscribing only to the condition factories
    /// that implement <c>IConditionChangeSource</c> would miss day changes entirely, because
    /// <c>DayAtLeastConditionFactory</c> is not one of them.
    /// </para>
    /// </summary>
    public sealed class ShopDecorAttentionSource : IAttentionSource, IDisposable
    {
        private readonly IShopService _shop;
        private readonly IDayProgressService _days;
        private readonly ILocationVisitChangeSource _visits;

        public ShopDecorAttentionSource(
            IShopService shop,
            IDayProgressService days = null,
            ILocationVisitChangeSource visits = null)
        {
            _shop = shop ?? throw new ArgumentNullException(nameof(shop));
            _days = days;
            _visits = visits;

            if (_days != null) _days.PhaseChanged += OnDayChanged;
            if (_visits != null) _visits.Changed += OnVisitsChanged;
        }

        public string Key => ShopAttentionKeys.Decor;

        public IEnumerable<string> CurrentIds
        {
            get
            {
                var lots = _shop.GetOfferedLots(NewspaperShopLotIds.StorefrontDecor);
                if (lots == null) yield break;

                for (var i = 0; i < lots.Count; i++)
                {
                    var lot = lots[i];
                    if (lot == null || string.IsNullOrEmpty(lot.LotId)) continue;
                    yield return lot.LotId;
                }
            }
        }

        public event Action Changed;

        public void Dispose()
        {
            if (_days != null) _days.PhaseChanged -= OnDayChanged;
            if (_visits != null) _visits.Changed -= OnVisitsChanged;
        }

        private void OnDayChanged(DayProgressState _) => Changed?.Invoke();
        private void OnVisitsChanged() => Changed?.Invoke();
    }
}
