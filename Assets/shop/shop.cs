    using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 简易商店管理器
/// - 使用 inspector 指定可售的面具目录（maskData 列表）
/// - 管理玩家灵魂（货币），提供购买接口 BuyMask / BuyMaskById
/// - 购买成功后会把面具添加到 maskmanager 的 deck（如可用）并触发事件用于 UI 刷新
/// 注意：maskData 在本项目中为 MonoBehaviour 类型，shopCatalog 应引用预制件或资源中带有 maskData 的对象引用。
/// </summary>
public class shop : MonoBehaviour
{
    /// <summary>
    /// 商店商品目录：在 Inspector 配置可售的面具（maskData 引用）
    /// </summary>
    public List<maskData> shopCatalog = new List<maskData>();

    /// <summary>
    /// 玩家当前灵魂数量（货币）
    /// Inspector 可设置初始值，运行时通过 API 修改
    /// </summary>
    public int playerSoul = 100;

    /// <summary>
    /// 刷新商店一次需要的灵魂消耗
    /// </summary>
    [Tooltip("刷新商店一次需要消耗的灵魂")]
    public int refreshCost = 20;

    /// <summary>
    /// 当前商店展位（最多 3 个），由 RefreshShop 填充
    /// </summary>
    private List<maskData> currentOffers = new List<maskData>(3);

    /// <summary>
    /// 可选：在 Inspector 绑定场景中的 maskmanager（如果不绑定，脚本会尝试自动查找）
    /// 购买后会把面具加入到该管理器的 deck
    /// </summary>
    public maskmanager maskManager;

    /// <summary>
    /// 在购买成功时触发（参数：被购买的 maskData）
    /// UI 可订阅此事件更新界面或播放特效
    /// </summary>
    public UnityEvent<maskData> OnPurchaseSuccess;

    /// <summary>
    /// 在货币变化时触发（参数：当前 playerSoul）
    /// UI 可订阅以刷新货币显示
    /// </summary>
    public UnityEvent<int> OnCurrencyChanged;

    /// <summary>
    /// 当商店刷新（展位更新）时触发（无参数）
    /// </summary>
    public UnityEvent OnShopRefreshed;

    void Awake()
    {
        // 尝试自动获取 maskmanager（如果未在 Inspector 指定）
        if (maskManager == null)
        {
            maskManager = FindObjectOfType<maskmanager>();
        }

        // 首次启动自动产生展位（不扣费）
        RefreshShop(force: true);
    }

    /// <summary>
    /// 检查玩家是否能购买该面具（根据 SoulCost）
    /// </summary>
    public bool CanAfford(maskData item)
    {
        if (item == null) return false;
        return playerSoul >= item.SoulCost;
    }

    /// <summary>
    /// 通过 maskData 引用购买面具（向后兼容）
    /// 如果该物品在 currentOffers 中，会把展位移除（不自动刷新）。
    /// 返回 true 表示购买成功（已扣费并加入 maskmanager）
    /// </summary>
    public bool BuyMask(maskData item)
    {
        if (item == null) return false;

        // 优先处理如果是当前展位的购买（保证展位移除）
        if (currentOffers.Contains(item))
        {
            return BuyOffer(item);
        }

        // 经典直接购买（仍会扣费并加入 maskManager）
        if (!CanAfford(item)) return false;

        playerSoul -= item.SoulCost;
        OnCurrencyChanged?.Invoke(playerSoul);

        if (maskManager != null)
        {
            if (maskManager.allMasks == null) maskManager.allMasks = new List<maskData>();
            if (!maskManager.allMasks.Contains(item)) maskManager.allMasks.Add(item);

            if (maskManager.deck == null) maskManager.deck = new List<maskData>();
            if (!maskManager.deck.Contains(item)) maskManager.deck.Add(item);
        }

        OnPurchaseSuccess?.Invoke(item);
        return true;
    }

    /// <summary>
    /// 购买当前展位中的物品：检查展位、检查货币、扣费、添加到 maskManager、从展位移除并触发事件。
    /// 购买成功后不会自动刷新展位（由玩家手动刷新或下一轮触发）。
    /// </summary>
    public bool BuyOffer(maskData item)
    {
        if (item == null) return false;
        if (!currentOffers.Contains(item)) return false;
        if (!CanAfford(item)) return false;

        // 扣费
        playerSoul -= item.SoulCost;
        OnCurrencyChanged?.Invoke(playerSoul);

        // 添加到 maskManager
        if (maskManager != null)
        {
            if (maskManager.allMasks == null) maskManager.allMasks = new List<maskData>();
            if (!maskManager.allMasks.Contains(item)) maskManager.allMasks.Add(item);

            if (maskManager.deck == null) maskManager.deck = new List<maskData>();
            if (!maskManager.deck.Contains(item)) maskManager.deck.Add(item);
        }

        // 从展位移除（不自动刷新）
        currentOffers.Remove(item);

        // 触发购买成功事件与可能的 UI 更新
        OnPurchaseSuccess?.Invoke(item);
        OnShopRefreshed?.Invoke(); // 让 UI 重新读取 currentOffers（虽然我们只移除一个）
        return true;
    }

    /// <summary>
    /// 刷新商店展位（生成最多 3 个随机面具）。
    /// 若 force 为 false，则会消耗 refreshCost 灵魂；force=true 则不扣费（用于初始化）。
    /// 返回是否刷新成功（主要用于判断是否有足够货币）
    /// </summary>
    public bool RefreshShop(bool force = false)
    {
        if (!force)
        {
            if (playerSoul < refreshCost) return false;
            playerSoul -= refreshCost;
            OnCurrencyChanged?.Invoke(playerSoul);
        }

        currentOffers.Clear();

        if (shopCatalog == null || shopCatalog.Count == 0)
        {
            OnShopRefreshed?.Invoke();
            return true;
        }

        // 若可选项 >=3：从中随机抽取不重复的 3 个
        if (shopCatalog.Count >= 3)
        {
            // 简单洗牌取前三
            List<maskData> pool = new List<maskData>(shopCatalog);
            for (int i = 0; i < pool.Count; i++)
            {
                int j = Random.Range(i, pool.Count);
                var tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }
            for (int k = 0; k < 3; k++) currentOffers.Add(pool[k]);
        }
        else
        {
            // 若少于3个，则允许重复选出直到凑齐3个（或也可改为只显示可用数量）
            for (int k = 0; k < 3; k++)
            {
                int idx = Random.Range(0, shopCatalog.Count);
                currentOffers.Add(shopCatalog[idx]);
            }
        }

        OnShopRefreshed?.Invoke();
        return true;
    }

    /// <summary>
    /// 通过面具 ID 购买（遍历 shopCatalog 查找第一个匹配的 MaskID）
    /// 返回是否购买成功
    /// </summary>
    public bool BuyMaskById(string maskId)
    {
        if (string.IsNullOrEmpty(maskId)) return false;
        foreach (var m in shopCatalog)
        {
            if (m != null && m.MaskID == maskId)
            {
                return BuyMask(m);
            }
        }
        return false;
    }

    /// <summary>
    /// 增加玩家灵魂（可用于奖励、充值等）
    /// </summary>
    public void AddPlayerSoul(int amount)
    {
        if (amount <= 0) return;
        playerSoul += amount;
        OnCurrencyChanged?.Invoke(playerSoul);
    }

    /// <summary>
    /// 减少玩家灵魂（安全调用，外部使用时建议先调用 CanAfford）
    /// </summary>
    public bool SpendPlayerSoul(int amount)
    {
        if (amount <= 0) return false;
        if (playerSoul < amount) return false;
        playerSoul -= amount;
        OnCurrencyChanged?.Invoke(playerSoul);
        return true;
    }

    /// <summary>
    /// 获取当前商店目录（只读副本） - 兼容旧逻辑（返回全部）
    /// </summary>
    public List<maskData> GetCatalog()
    {
        return new List<maskData>(shopCatalog);
    }

    /// <summary>
    /// 获取当前展位（只读副本），用于 UI 渲染当前 3 个项目
    /// </summary>
    public List<maskData> GetCurrentOffers()
    {
        return new List<maskData>(currentOffers);
    }
}
