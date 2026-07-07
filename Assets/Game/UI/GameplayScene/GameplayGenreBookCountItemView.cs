using System;
using Game.Configs.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameplayGenreBookCountItemView : MonoBehaviour
{
    [SerializeField] private Image _genreImage;
    [SerializeField] private TMP_Text _countText;
    [SerializeField] private RectTransform _purchasedAmountGameObject;
    [SerializeField] private TMP_Text _purchasedAmountText;
    [SerializeField] private Button _button;

    private Action<GameplayGenreBookCountItemView> _onClick;

    public BookGenre Genre { get; private set; }
    public RectTransform RectTransform => transform as RectTransform;

    private void Awake()
    {
        if (_button != null)
            _button.onClick.AddListener(OnClicked);
    }

    public void Bind(
        BookGenre genre,
        Sprite genreSprite,
        int count,
        int purchasedAmount = 0,
        bool showPurchased = false,
        Action<GameplayGenreBookCountItemView> onClick = null)
    {
        Genre = genre;
        _onClick = onClick;
        SetSprite(genreSprite);
        SetCount(count);
        SetPurchasedAmount(purchasedAmount, showPurchased);
    }

    public void SetCount(int count)
    {
        if (_countText != null)
            _countText.text = count.ToString();
    }

    public void SetSprite(Sprite sprite)
    {
        if (_genreImage != null)
            _genreImage.sprite = sprite;
    }

    public void SetPurchasedAmount(int amount, bool visible)
    {
        // Бейдж «-N» с числом проданных книг этого жанра. Показывается только когда есть контекст
        // продаж (visible = ShowPurchasedCounts, true лишь в LocationScene) и куплена хотя бы 1 книга.
        // Во всех остальных случаях объект выключен.
        var shouldShow = visible && amount > 0;

        if (_purchasedAmountGameObject != null)
            _purchasedAmountGameObject.gameObject.SetActive(shouldShow);

        if (_purchasedAmountText != null)
        {
            _purchasedAmountText.gameObject.SetActive(shouldShow);
            if (shouldShow)
                _purchasedAmountText.text = $"-{amount}";
        }
    }

    private void OnClicked() => _onClick?.Invoke(this);

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnClicked);
    }
}
