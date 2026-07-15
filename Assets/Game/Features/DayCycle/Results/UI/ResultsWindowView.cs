using System.Collections.Generic;
using Game.Newspaper.UI;
using Game.Rewards.UI;
using Game.UI;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.DayCycle.Results.UI
{
    public sealed class ResultsWindowView : WindowView
    {
        [Header("Results")]
        [SerializeField] private TMP_Text _earnedGoldLabel;
        [SerializeField] private RectTransform _coinFlightSource;

        [Header("Sold genres")]
        [SerializeField] private UIListPool<RewardItemView> _soldGenrePool = new();

        [Header("Actions")]
        [SerializeField] private Button _nextDayButton;

        private readonly Dictionary<RewardSpecResource, RewardItemView> _soldGenreViews = new();

        public Button NextDayButton => _nextDayButton;

        public void SetEarnedGold(int amount)
        {
            if (_earnedGoldLabel != null)
                _earnedGoldLabel.text = Mathf.Max(0, amount).ToString();
        }

        public void SetSoldGenres(IReadOnlyList<RewardSpecResource> soldGenres)
        {
            ResetSoldGenres();
            if (soldGenres == null || _soldGenrePool == null) return;

            for (var i = 0; i < soldGenres.Count; i++)
            {
                var resource = soldGenres[i];
                if (resource == null) continue;

                var view = _soldGenrePool.GetNext();
                view.SetResourceData(resource);
                _soldGenreViews[resource] = view;
            }

            _soldGenrePool.DisableNonActive();
        }

        public IReadOnlyDictionary<RewardSpecResource, RewardItemView> GetSoldGenreViews()
            => _soldGenreViews;

        public void ResetView()
        {
            SetEarnedGold(0);
            ResetSoldGenres();
        }

        public bool TryGetCoinFlightSourceScreenPoint(out Vector2 screenPoint)
        {
            var source = _coinFlightSource != null
                ? _coinFlightSource
                : _earnedGoldLabel != null
                    ? _earnedGoldLabel.rectTransform
                    : transform as RectTransform;

            if (source == null)
            {
                screenPoint = default;
                return false;
            }

            screenPoint = RectTransformUtility.WorldToScreenPoint(null, source.position);
            return true;
        }

        private void ResetSoldGenres()
        {
            foreach (var view in _soldGenreViews.Values)
                if (view != null) view.ResetView();

            _soldGenreViews.Clear();
            _soldGenrePool?.DisableAll();
        }
    }
}
