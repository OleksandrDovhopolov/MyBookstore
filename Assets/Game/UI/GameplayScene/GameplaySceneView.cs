using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.UI;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

public class GameplaySceneView : WindowView
{
    [SerializeField] private TextMeshProUGUI _goldAmountText;
    [SerializeField] private GameObject _salesGoldRoot;
    [SerializeField] private TMP_Text _salesGoldLabel;
    [SerializeField] private TMP_Text _dayLabel;

    [Header("Shop entry")]
    [SerializeField] private Button _cheatButton;
    [SerializeField] private Button _startDayButton;
    [SerializeField] private Button _decorButton;

    [Header("Genre book counts")]
    [SerializeField] private UIListPool<GameplayGenreBookCountItemView> _genreBookCountPool = new();

    private readonly Dictionary<BookGenre, Sprite> _genreSprites = new();

    private bool _legacyGenreBookCountItemsHidden;

    private AnimatedShowHidePanel[] _animatedPanels;

    public Button StartDayButton => _startDayButton;
    public Button DecorButton => _decorButton;

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

    private UniTask RunPanelsAsync(bool show, bool instant)
    {
        var panels = AnimatedPanels;
        var tasks = new List<UniTask>(panels.Length);

        foreach (var panel in panels)
        {
            if (panel == null) continue;

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
        HideLegacyGenreBookCountItemsIfNeeded();
        SetSalesGoldVisible(false);
    }

    public void SetSceneButtonsInteractable(bool interactable)
    {
        if (_cheatButton != null)
        {
            _cheatButton.interactable = interactable;
            _cheatButton.gameObject.SetActive(interactable);
        }

        if (_decorButton != null)
        {
            _decorButton.interactable = interactable;
            _decorButton.gameObject.SetActive(interactable);
        }

        SetStartButtonActive(interactable);
    }

    public void SetStartButtonActive(bool active)
    {
        if (_startDayButton != null)
        {
            _startDayButton.interactable = active;
            _startDayButton.gameObject.SetActive(active);
        }
    }

    public void SetGoldAmount(int goldAmount)
    {
        _goldAmountText.text = goldAmount.ToString();
    }

    public void SetSalesGoldAmount(int amount)
    {
        if (_salesGoldLabel != null)
            _salesGoldLabel.text = amount.ToString();
    }

    public void SetSalesGoldVisible(bool visible)
    {
        if (_salesGoldRoot != null)
            _salesGoldRoot.gameObject.SetActive(visible);
    }
    
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
            item.Bind(genre, ResolveGenreSprite(genre), pair.Value, purchasedAmount, showPurchasedCounts);
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

}
