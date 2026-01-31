using UnityEngine;
using UnityEngine.UI;
using TMPro;
/// <summary>
/// 单个商店项 UI 组件（绑定到商品预制件）
/// - 负责显示图标、名称、价格以及购买按钮的交互
/// - 通过 Init 绑定数据和商店管理器，自动响应货币变动
/// </summary>
public class ShopItemUI : MonoBehaviour
{
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text priceText;
    public Button buyButton;

    private maskData itemData;
    private shop shopManager;

    public void Init(maskData data, shop manager)
    {
        itemData = data;
        shopManager = manager;

        if (icon != null) icon.sprite = data != null ? data.maskImage : null;
        if (nameText != null) nameText.text = data != null ? data.maskName : "Unknown";
        if (priceText != null) priceText.text = data != null ? data.SoulCost.ToString() : "0";

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnBuyClicked);
        }

        // 监听货币变化以更新按钮可交互状态
        if (shopManager != null)
        {
            shopManager.OnCurrencyChanged.AddListener(UpdateInteractable);
            UpdateInteractable(shopManager.playerSoul);
        }
    }

    void OnDestroy()
    {
        if (shopManager != null)
        {
            shopManager.OnCurrencyChanged.RemoveListener(UpdateInteractable);
        }
    }

    private void OnBuyClicked()
    {
        if (shopManager == null || itemData == null) return;
        bool ok = shopManager.BuyMask(itemData);
        if (!ok)
        {
            // 购买失败（可扩展为提示 UI）
            Debug.Log($"无法购买 {itemData.maskName}（灵魂不足或其他原因）");
        }
        else
        {
            // 购买成功后刷新按钮状态（或禁用）
            UpdateInteractable(shopManager.playerSoul);
        }
    }

    private void UpdateInteractable(int currentSoul)
    {
        if (buyButton == null || itemData == null) return;
        buyButton.interactable = currentSoul >= itemData.SoulCost;
    }
  
}