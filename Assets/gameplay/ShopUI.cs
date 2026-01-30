using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 商店 UI 控制器：负责根据 shop.catalog 动态生成商品列表
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
        Refresh();
    }

    public void Refresh()
    {
        ClearList();
        if (shopManager == null || itemPrefab == null || contentParent == null) return;

        List<maskData> catalog = shopManager.GetCatalog();
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
}