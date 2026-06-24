using Game.Configs.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameplayGenreBookCountItemView : MonoBehaviour
{
    [SerializeField] private Image _genreImage;
    [SerializeField] private TMP_Text _countText;
    [SerializeField] private TMP_Text _purchasedAmountText;

    public BookGenre Genre { get; private set; }

    public void Bind(BookGenre genre, Sprite genreSprite, int count, int purchasedAmount = 0, bool showPurchased = false)
    {
        Genre = genre;
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
        if (_purchasedAmountText == null) return;

        var shouldShow = visible && amount > 0;
        _purchasedAmountText.gameObject.SetActive(shouldShow);
        if (shouldShow)
            _purchasedAmountText.text = $"-{amount}";
    }
}
