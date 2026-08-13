using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.UI;
using Game.UI.ContentWidget;
using TMPro;
using UIShared;
using UnityEngine;

namespace GameplayUI
{
    public class GameplaySceneView : WindowView
    {
        [SerializeField] private TMP_Text _dayLabel;
        [SerializeField] private HudMenuButtonsView _menuButtons;

        [Header("Gold counters")]
        [SerializeField] private GameObject _hubGoldCounterRoot;
        [SerializeField] private ResourceCounterTargetTag _hubGoldCounter;
        [SerializeField] private GameObject _locationGoldCounterRoot;
        [SerializeField] private ResourceCounterTargetTag _locationGoldCounter;

        [Header("Genre book counts")] [SerializeField]
        private UIListPool<GameplayGenreBookCountItemView> _genreBookCountPool = new();

        [SerializeField] private SaleChanceWidgetView _saleChanceWidgetPrefab;

        private readonly Dictionary<BookGenre, Sprite> _genreSprites = new();

        private bool _legacyGenreBookCountItemsHidden;

        private AnimatedShowHidePanel[] _animatedPanels;

        public HudMenuButtonsView MenuButtons => _menuButtons;
        public event Action<BookGenre, Sprite, RectTransform> GenreItemClicked;

        // Collected from the view hierarchy at runtime (including inactive) so any number of
        // panels — top / side / bottom / any future ones — is driven together without wiring
        // each one by hand in the inspector. Cached on first use since the HUD hierarchy is static.
        private AnimatedShowHidePanel[] AnimatedPanels =>
            _animatedPanels ??= GetComponentsInChildren<AnimatedShowHidePanel>(includeInactive: true);

        // Completes only after every panel has finished its show/hide tween, so callers can await
        // the animation before doing the next thing (e.g. opening a window on top). Bound to the
        // view's destroy token so it never hangs if the HUD is torn down mid-animation.
        public UniTask ShowAnimatedPanelsAsync(bool instant = false) => RunPanelsAsync(show: true, instant);

        public UniTask HideAnimatedPanelsAsync(bool instant = false) => RunPanelsAsync(show: false, instant);

        // Shows/hides a single panel identified by its PanelId, leaving the others untouched. Used to drive
        // the genre panel from the location state without affecting the top/side/bottom chrome panels.
        public void SetPanelShown(AnimatedShowHidePanel.PanelId id, bool shown, bool instant = false)
        {
            if (id == AnimatedShowHidePanel.PanelId.None) return;

            foreach (var panel in AnimatedPanels)
            {
                if (panel == null || panel.Id != id) continue;

                if (shown)
                    panel.Show(instant);
                else
                    panel.Hide(instant);
            }
        }

        private UniTask RunPanelsAsync(bool show, bool instant)
        {
            var panels = AnimatedPanels;
            var tasks = new List<UniTask>(panels.Length);

            foreach (var panel in panels)
            {
                if (panel == null) continue;

                // The genre panel's visibility is owned by the location state, not the generic show/hide-all
                // flow (used by the Decor window), so the generic flow must never touch it.
                if (panel.Id == AnimatedShowHidePanel.PanelId.GenreBookCounts) continue;

                var completion = new UniTaskCompletionSource();
                if (show)
                    panel.Show(instant, () => completion.TrySetResult());
                else
                    panel.Hide(instant, () => completion.TrySetResult());

                tasks.Add(completion.Task);
            }

            return UniTask.WhenAll(tasks).AttachExternalCancellation(destroyCancellationToken);
        }

        protected override void Awake()
        {
            base.Awake();
            if (_saleChanceWidgetPrefab != null)
                WidgetRegistry.Register<SaleChanceWidgetData>(_saleChanceWidgetPrefab);

            HideLegacyGenreBookCountItemsIfNeeded();
            ResolveGoldCounterReferences();
        }

        public void SetGoldCounterMode(bool locationLoaded)
        {
            ResolveGoldCounterReferences();

            if (_hubGoldCounterRoot != null)
                _hubGoldCounterRoot.SetActive(!locationLoaded);

            if (_locationGoldCounterRoot != null)
                _locationGoldCounterRoot.SetActive(locationLoaded);
        }

        public void SetLocationEarnedGold(int amount)
        {
            ResolveGoldCounterReferences();
            _locationGoldCounter?.SetAmountImmediate(amount);
        }

        public void SetSceneButtonsInteractable(bool interactable)
        {
            _menuButtons?.SetInteractable(interactable);
        }

        public void SetStartButtonActive(bool active) => _menuButtons?.SetStartButtonActive(active);

        public void SetDayText(string value)
        {
            if (_dayLabel != null)
                _dayLabel.text = value ?? string.Empty;
        }

        public void SetGenreBookCounts(IReadOnlyDictionary<string, int> counts)
            => SetGenreBookCounts(counts, null, false);

        public void SetGenreBookCounts(
            IReadOnlyDictionary<string, int> counts,
            IReadOnlyDictionary<string, int> purchasedCounts,
            bool showPurchasedCounts)
        {
            if (_genreBookCountPool == null) return;

            HideLegacyGenreBookCountItemsIfNeeded();
            _genreBookCountPool.DisableAll();

            var normalizedCounts = BookGenreCounts.Normalize(counts);
            var normalizedPurchasedCounts = BookGenreCounts.Normalize(purchasedCounts);
            foreach (var pair in normalizedCounts)
            {
                if (!BookGenreExtensions.TryParseGenre(pair.Key, out var genre))
                    continue;

                var item = _genreBookCountPool.GetNext();
                normalizedPurchasedCounts.TryGetValue(pair.Key, out var purchasedAmount);
                item.Bind(
                    genre,
                    ResolveGenreSprite(genre),
                    pair.Value,
                    purchasedAmount,
                    showPurchasedCounts,
                    OnGenreItemClicked);
            }

            _genreBookCountPool.DisableNonActive();
        }

        private void HideLegacyGenreBookCountItemsIfNeeded()
        {
            if (_legacyGenreBookCountItemsHidden) return;

            var parent = _genreBookCountPool?.Parent;
            if (parent == null) return;

            for (var i = 0; i < parent.childCount; i++)
                parent.GetChild(i).gameObject.SetActive(false);

            _legacyGenreBookCountItemsHidden = true;
        }

        public void SetGenreSprites(IReadOnlyDictionary<BookGenre, Sprite> sprites)
        {
            _genreSprites.Clear();
            if (sprites != null)
                foreach (var kv in sprites)
                    _genreSprites[kv.Key] = kv.Value;

            if (_genreBookCountPool == null) return;

            foreach (var item in _genreBookCountPool.ActiveElements())
                if (item != null)
                    item.SetSprite(ResolveGenreSprite(item.Genre));
        }

        private Sprite ResolveGenreSprite(BookGenre genre)
            => _genreSprites.TryGetValue(genre, out var sprite) ? sprite : null;

        private void ResolveGoldCounterReferences()
        {
            if (_hubGoldCounter != null && _hubGoldCounterRoot == null)
                _hubGoldCounterRoot = _hubGoldCounter.gameObject;

            if (_locationGoldCounter != null && _locationGoldCounterRoot == null)
                _locationGoldCounterRoot = _locationGoldCounter.gameObject;
        }

        private void OnGenreItemClicked(GameplayGenreBookCountItemView item)
        {
            if (item == null) return;
            GenreItemClicked?.Invoke(item.Genre, ResolveGenreSprite(item.Genre), item.RectTransform);
        }
    }
}
