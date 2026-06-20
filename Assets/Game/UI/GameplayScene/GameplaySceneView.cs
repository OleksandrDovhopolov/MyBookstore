using System.Collections.Generic;
using System.Linq;
using Game.DayCycle.Morning.UI;
using Game.Preparation.UI;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameplaySceneView : WindowView
{
    [SerializeField] private Image _goldImage;
    [SerializeField] private TextMeshProUGUI _goldAmountText;

    [Header("Shop entry")]
    [SerializeField] private Button _openShopButton;
    [SerializeField] private Button _cheatButton;
    [SerializeField] private Button _startDayButton;

    [Header("Genre book counts")]
    [SerializeField] private List<GameplayGenreBookCountItemView> _genreBookCountItems = new();

    //TODO remove it from here when migrate from this views
    /*[Header("Next screen")]
    [SerializeField] private GameObject _preparationScreenRoot;
    [SerializeField] private GameObject _morningScreenRoot;*/

    public Button OpenShopButton => _openShopButton;
    public Button StartDayButton => _startDayButton;


    //TODO remove it from here when migrate from this views
    private GameObject _preparationScreenRoot;
    private GameObject _morningScreenRoot;
    public GameObject PreparationScreenRoot
    {
        get
        {
            if (_preparationScreenRoot == null)
            {
                var result = GameObject.FindObjectsByType<PreparationScreenView>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                _preparationScreenRoot = result.FirstOrDefault()?.gameObject;
            }

            return _preparationScreenRoot;
        }
    }

    public GameObject MorningScreenRoot
    {
        get
        {
            if (_morningScreenRoot == null)
            {
                var result = GameObject.FindObjectsByType<MorningScreenView>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                _morningScreenRoot = result.FirstOrDefault()?.gameObject;
            }

            return _morningScreenRoot;
        }
    }

    public void SetSceneButtonsInteractable(bool interactable)
    {
        if (_openShopButton != null)
        {
            _openShopButton.interactable = interactable;
            _openShopButton.gameObject.SetActive(interactable);
        }

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
