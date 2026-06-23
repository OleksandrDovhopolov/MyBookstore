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
    [SerializeField] private Sprite _classicGenreSprite;
    [SerializeField] private Sprite _crimeGenreSprite;
    [SerializeField] private Sprite _dramaGenreSprite;
    [SerializeField] private Sprite _factGenreSprite;
    [SerializeField] private Sprite _kidsGenreSprite;
    [SerializeField] private Sprite _travelGenreSprite;
    [SerializeField] private Sprite _fantasyGenreSprite;

    private bool _legacyGenreBookCountItemsHidden;

    public Button StartDayButton => _startDayButton;

    private void Awake()
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
    {
        if (_genreBookCountPool == null) return;

        HideLegacyGenreBookCountItemsIfNeeded();
        _genreBookCountPool.DisableAll();

        var normalizedCounts = BookGenreCounts.Normalize(counts);
        foreach (var pair in normalizedCounts)
        {
            if (!BookGenreExtensions.TryParseGenre(pair.Key, out var genre))
                continue;

            var item = _genreBookCountPool.GetNext();
            item.Bind(genre, GetGenreSprite(genre), pair.Value);
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

    private Sprite GetGenreSprite(BookGenre genre)
    {
        switch (genre)
        {
            case BookGenre.Classic:
                return _classicGenreSprite;
            case BookGenre.Crime:
                return _crimeGenreSprite;
            case BookGenre.Drama:
                return _dramaGenreSprite;
            case BookGenre.Fact:
                return _factGenreSprite;
            case BookGenre.Kids:
                return _kidsGenreSprite;
            case BookGenre.Travel:
                return _travelGenreSprite;
            case BookGenre.Fantasy:
                return _fantasyGenreSprite;
            default:
                return null;
        }
    }

}
