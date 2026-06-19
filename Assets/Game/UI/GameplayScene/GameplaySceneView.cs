using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameplaySceneView : WindowView
{
    [SerializeField] private Image _goldImage;
    [SerializeField] private TextMeshProUGUI _goldAmountText;
    [SerializeField] private Image _reputationImage;
    [SerializeField] private TextMeshProUGUI _reputationAmountText;

    [Header("Shop entry")]
    [SerializeField] private Button _openShopButton;
    [SerializeField] private Button _cheatButton;

    public Button OpenShopButton => _openShopButton;

    public void SetSceneButtonsInteractable(bool interactable)
    {
        if (_openShopButton != null)
        {
            _openShopButton.interactable = interactable;
            _openShopButton.gameObject.SetActive(interactable);
        }
        
        if (_cheatButton != null)
        {
            _openShopButton.interactable = interactable;
            _cheatButton.gameObject.SetActive(interactable);
        }
    }

    public void SetGoldAmount(int goldAmount)
    {
        _goldAmountText.text = goldAmount.ToString();
    }

    public void SetReputationAmount(int goldAmount)
    {
        _reputationAmountText.text = goldAmount.ToString();
    }
}
