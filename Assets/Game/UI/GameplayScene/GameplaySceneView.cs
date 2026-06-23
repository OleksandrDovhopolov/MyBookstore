using System.Collections.Generic;
using Game.UI;
using TMPro;
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
    [SerializeField] private List<GameplayGenreBookCountItemView> _genreBookCountItems = new();

    public Button StartDayButton => _startDayButton;

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
        if (_genreBookCountItems == null) return;

        for (var i = 0; i < _genreBookCountItems.Count; i++)
        {
            var item = _genreBookCountItems[i];
            if (item == null) continue;

            var count = 0;
            if (counts != null && !string.IsNullOrEmpty(item.GenreId))
                counts.TryGetValue(item.GenreId, out count);

            item.SetCount(count);
        }
    }
}
