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

    public void SetGoldAmount(int goldAmount)
    {
        _goldAmountText.text = goldAmount.ToString();
    }
    
    public void SetReputationAmount(int goldAmount)
    {
        _reputationAmountText.text = goldAmount.ToString();
    }
}
