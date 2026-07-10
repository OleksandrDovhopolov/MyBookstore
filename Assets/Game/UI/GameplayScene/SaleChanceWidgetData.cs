using Game.Configs.Models;
using Game.UI.ContentWidget;
using UnityEngine;

public sealed class SaleChanceWidgetData : ContentWidgetDataBase
{
    public BookGenre Genre { get; }
    public int Percent { get; }
    public Sprite GenreSprite { get; }

    public SaleChanceWidgetData(BookGenre genre, int percent, Sprite genreSprite)
    {
        Genre = genre;
        Percent = percent;
        GenreSprite = genreSprite;
    }
}
