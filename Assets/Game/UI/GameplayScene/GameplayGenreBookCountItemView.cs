using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameplayGenreBookCountItemView : MonoBehaviour
{
    [SerializeField] private string _genreId;
    [SerializeField] private Image _genreImage;
    [SerializeField] private Sprite _genreSprite;
    [SerializeField] private TMP_Text _countText;

    public string GenreId => _genreId;

    private void Awake()
    {
        ApplySprite();
        SetCount(0);
    }

    private void OnValidate()
    {
        ApplySprite();
    }

    public void SetCount(int count)
    {
        if (_countText != null)
            _countText.text = count.ToString();
    }

    private void ApplySprite()
    {
        if (_genreImage != null)
            _genreImage.sprite = _genreSprite;
    }
}
