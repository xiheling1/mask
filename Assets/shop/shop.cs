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

    void Awake()
    {
        // 尝试自动获取 maskmanager（如果未在 Inspector 指定）
        if (maskManager == null)
        {
            maskManager = FindObjectOfType<maskmanager>();
        }
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
    /// 通过 maskData 引用购买面具
    /// 返回 true 表示购买成功（已扣费并加入 maskmanager）
    /// </summary>
    public bool BuyMask(maskData item)
    {
        if (item == null) return false;

        // 是否可购买
        if (!CanAfford(item)) return false;

        // 扣费
        playerSoul -= item.SoulCost;
        OnCurrencyChanged?.Invoke(playerSoul);

        // 将面具添加到 maskmanager 的集合中（避免重复）
        if (maskManager != null)
        {
            if (maskManager.allMasks == null) maskManager.allMasks = new List<maskData>();
            if (!maskManager.allMasks.Contains(item))
            {
                maskManager.allMasks.Add(item);
            }

            if (maskManager.deck == null) maskManager.deck = new List<maskData>();
            if (!maskManager.deck.Contains(item))
            {
                maskManager.deck.Add(item);
            }
        }

        // 触发购买成功事件
        OnPurchaseSuccess?.Invoke(item);
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
    /// 获取当前商店目录（只读副本）
    /// </summary>
    public List<maskData> GetCatalog()
    {
        return new List<maskData>(shopCatalog);
    }
}
