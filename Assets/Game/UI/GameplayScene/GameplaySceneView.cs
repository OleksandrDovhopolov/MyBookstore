using System;
using System.Collections.Generic;
using Game.Configs.Models;
using Game.UI;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

public class GameplaySceneView : WindowView
{
    [SerializeField] private Image _goldImage;
    [SerializeField] private TextMeshProUGUI _goldAmountText;
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

        var genres = BuildGenresToDisplay(counts);
        for (var i = 0; i < genres.Count; i++)
        {
            var genre = genres[i];
            var item = _genreBookCountPool.GetNext();
            item.Bind(genre, GetGenreSprite(genre), ResolveGenreCount(counts, genre));
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

    private static int ResolveGenreCount(IReadOnlyDictionary<string, int> counts, BookGenre genre)
    {
        if (counts == null)
            return 0;

        var genreId = genre.ToConfigValue();
        if (counts.TryGetValue(genreId, out var count))
            return count;

        foreach (var pair in counts)
            if (string.Equals(pair.Key, genreId, StringComparison.OrdinalIgnoreCase))
                return pair.Value;

        return 0;
    }

    private static List<BookGenre> BuildGenresToDisplay(IReadOnlyDictionary<string, int> counts)
    {
        var result = new List<BookGenre>();
        var added = new HashSet<BookGenre>();

        if (counts != null)
        {
            foreach (var pair in counts)
            {
                if (!BookGenreExtensions.TryParseGenre(pair.Key, out var genre) || !added.Add(genre))
                    continue;

                result.Add(genre);
            }
        }

        foreach (BookGenre genre in Enum.GetValues(typeof(BookGenre)))
        {
            if (added.Add(genre))
                result.Add(genre);
        }

        return result;
    }
}
