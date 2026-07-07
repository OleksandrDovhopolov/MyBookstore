using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI.ContentWidget;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SaleChanceWidgetView : MonoBehaviour, IContentWidgetView
{
    [SerializeField] private Image _genreIcon;
    [SerializeField] private TMP_Text _genreLabel;
    [SerializeField] private TMP_Text _percentLabel;

    public bool Setup(ContentWidgetDataBase data)
    {
        if (data is not SaleChanceWidgetData saleChance)
            return false;

        if (_genreIcon != null)
            _genreIcon.sprite = saleChance.GenreSprite;

        if (_genreLabel != null)
            _genreLabel.text = saleChance.Genre.ToString();

        if (_percentLabel != null)
            _percentLabel.text = $"{saleChance.Percent}%";

        return true;
    }

    public UniTask OnViewCreatedAsync(CancellationToken ct) => UniTask.CompletedTask;
}
