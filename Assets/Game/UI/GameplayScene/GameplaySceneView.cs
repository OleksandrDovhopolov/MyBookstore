using System.Collections.Generic;
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

    [Header("Genre book counts")]
    [SerializeField] private UIListPool<GameplayGenreBookCountItemView> _genreBookCountPool = new();

    private readonly Dictionary<BookGenre, Sprite> _genreSprites = new();

    private bool _legacyGenreBookCountItemsHidden;

    public Button StartDayButton => _startDayButton;

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

    /// <summary>
    /// Receives genre sprites loaded by the controller (from Addressables). Re-applies them to any
    /// items already spawned, since genre counts may have been bound before sprites finished loading.
    /// </summary>
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
