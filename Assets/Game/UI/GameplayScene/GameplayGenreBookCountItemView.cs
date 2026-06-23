using Game.Configs.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameplayGenreBookCountItemView : MonoBehaviour
{
    [SerializeField] private Image _genreImage;
    [SerializeField] private TMP_Text _countText;

    public BookGenre Genre { get; private set; }

    public void Bind(BookGenre genre, Sprite genreSprite, int count)
    {
        Genre = genre;
        SetSprite(genreSprite);
        SetCount(count);
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
}
