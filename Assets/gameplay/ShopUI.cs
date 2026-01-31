using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 商店 UI 控制器：负责根据 shop 的当前展位动态生成商品列表
/// - 将 itemPrefab（含 ShopItemUI）实例化到 contentParent 下
/// - 在 Inspector 中设置 prefab、父容器与 shop 关联
/// </summary>
public class ShopUI : MonoBehaviour
{
    public shop shopManager;
    public GameObject itemPrefab; // 预制件需包含 ShopItemUI 组件并设置 icon/name/price/button
    public Transform contentParent; // 列表的父容器（例如 ScrollView content）

    private List<GameObject> spawnedItems = new List<GameObject>();

    void Start()
    {
        if (shopManager == null) shopManager = FindObjectOfType<shop>();
        if (shopManager != null)
        {
            // 监听商店刷新与货币变化，更新 UI
            shopManager.OnShopRefreshed.AddListener(Refresh);
            shopManager.OnCurrencyChanged.AddListener((_) => { /* 可用于刷新按钮状态 */ });
        }
        Refresh();
    }

    public void Refresh()
    {
        ClearList();
        if (shopManager == null || itemPrefab == null || contentParent == null) return;

        // 优先使用当前展位（3 个），若为空回退到完整目录（兼容旧逻辑）
        List<maskData> catalog = shopManager.GetCurrentOffers();
        if (catalog == null || catalog.Count == 0)
        {
            catalog = shopManager.GetCatalog();
        }

        foreach (var item in catalog)
        {
            if (item == null) continue;
            GameObject go = Instantiate(itemPrefab, contentParent);
            var ui = go.GetComponent<ShopItemUI>();
            if (ui != null) ui.Init(item, shopManager);
            spawnedItems.Add(go);
        }
    }

    public void ClearList()
    {
        foreach (var go in spawnedItems) if (go != null) Destroy(go);
        spawnedItems.Clear();
    }

    // UI 按钮可以调用这个方法手动刷新商店（会尝试扣费）
    public void OnRefreshButtonPressed()
    {
        if (shopManager == null) return;
        bool ok = shopManager.RefreshShop(force:false);
        if (!ok)
        {
            Debug.Log("刷新失败：灵魂不足");
        }
    }
}