using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI.ContentWidget;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SaleChanceWidgetView : MonoBehaviour, IContentWidgetView
{
    [SerializeField] private TMP_Text _percentLabel;
    [SerializeField] private Button _closeButton;

    private void Awake()
    {
        if (_closeButton != null)
            _closeButton.onClick.AddListener(OnCloseClicked);
    }

    public bool Setup(ContentWidgetDataBase data)
    {
        if (data is not SaleChanceWidgetData saleChance)
            return false;

        if (_percentLabel != null)
            _percentLabel.text = $"{saleChance.Percent}%";

        return true;
    }

    public UniTask OnViewCreatedAsync(CancellationToken ct) => UniTask.CompletedTask;

    private void OnCloseClicked()
    {
        GetComponentInParent<ContentWidgetView>()?.RequestClose();
    }

    private void OnDestroy()
    {
        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(OnCloseClicked);
    }
}
