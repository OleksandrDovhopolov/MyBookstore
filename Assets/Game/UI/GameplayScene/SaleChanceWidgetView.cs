using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI.ContentWidget;
using TMPro;
using UnityEngine;

public sealed class SaleChanceWidgetView : MonoBehaviour, IContentWidgetView
{
    [SerializeField] private TMP_Text _percentLabel;

    public bool Setup(ContentWidgetDataBase data)
    {
        if (data is not SaleChanceWidgetData saleChance)
            return false;

        if (_percentLabel != null)
            _percentLabel.text = $"{saleChance.Percent}%";

        return true;
    }

    public UniTask OnViewCreatedAsync(CancellationToken ct) => UniTask.CompletedTask;
}
