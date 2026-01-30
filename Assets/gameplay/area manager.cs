using System.Collections.Generic;
using UnityEngine;

public class MaskOverlapManager : MonoBehaviour
{
    public static MaskOverlapManager Instance { get; private set; }

    // 已注册的所有面具（MaskCardUI）
    private List<MaskCardUI> registeredMasks = new List<MaskCardUI>();

    // 拖拽结束时吸附阈值（世界/UI 单位）
    [Tooltip("拖拽结束时与槽位距离小于此值才会吸附")]
    public float snapDistance = 80f;

    // 槽位半径：宿主面具局部方向乘以此值得到槽位世界偏移
    [Tooltip("槽位与宿主中心的距离")]
    public float slotDistance = 60f;

    // 八方向局部向量（顺时针或逆时针都可）
    private static readonly Vector3[] slotDirs = new Vector3[]
    {
        new Vector3(0, 1, 0),    // 上
        new Vector3(1, 1, 0).normalized, // 右上
        new Vector3(1, 0, 0),    // 右
        new Vector3(1, -1, 0).normalized, // 右下
        new Vector3(0, -1, 0),   // 下
        new Vector3(-1, -1, 0).normalized, // 左下
        new Vector3(-1, 0, 0),   // 左
        new Vector3(-1, 1, 0).normalized  // 左上
    };

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void RegisterMask(MaskCardUI card)
    {
        if (card == null) return;
        if (!registeredMasks.Contains(card)) registeredMasks.Add(card);
    }

    public void UnregisterMask(MaskCardUI card)
    {
        if (card == null) return;
        registeredMasks.Remove(card);
    }

    /// <summary>
    /// 查找最近可用槽位（遍历所有已注册面具的 8 个槽位）
    /// 返回 true 表示找到了合适槽位，并输出宿主、槽索引及槽位世界坐标
    /// </summary>
    public bool FindNearestFreeSlot(Vector2 draggedWorldPos, MaskCardUI dragging, out MaskCardUI host, out int slotIndex, out Vector3 slotWorldPos)
    {
        host = null;
        slotIndex = -1;
        slotWorldPos = Vector3.zero;

        float bestDist = float.MaxValue;
        foreach (var h in registeredMasks)
        {
            if (h == null || h == dragging) continue;

            for (int i = 0; i < slotDirs.Length; i++)
            {
                if (!h.IsSlotFree(i)) continue;

                // 计算宿主槽位世界坐标（局部方向 * slotDistance）
                Vector3 localOffset = slotDirs[i] * slotDistance;
                Vector3 worldPos = h.transform.TransformPoint(localOffset);

                float dist = Vector2.Distance(draggedWorldPos, (Vector2)worldPos);
                if (dist < snapDistance && dist < bestDist)
                {
                    bestDist = dist;
                    host = h;
                    slotIndex = i;
                    slotWorldPos = worldPos;
                }
            }
        }

        return host != null;
    }

    /// <summary>
    /// 把卡片吸附到宿主的指定槽位（会处理旧宿主清理、设置父对象、更新槽占用）。
    /// 吸附后会触发双方的重叠计算与加成应用。
    /// </summary>
    public void SnapToSlot(MaskCardUI host, int slotIndex, MaskCardUI card)
    {
        if (host == null || card == null) return;
        // 先从原宿主解绑
        if (card.attachedHost != null)
        {
            // 当解绑时，MaskCardUI 会在 ClearSlot 中移除相关加成
            card.attachedHost.ClearSlot(card.attachedSlotIndex);
        }

        // 计算槽位世界位置并设置
        Vector3 worldPos = host.transform.TransformPoint(slotDirs[slotIndex] * slotDistance);

        // 设置层级（把卡片作为宿主子对象，便于一起移动/旋转）
        card.transform.SetParent(host.transform, worldPositionStays: true);
        card.transform.position = worldPos;

        // 更新双方关系
        host.SetSlot(slotIndex, card);
        card.attachedHost = host;
        card.attachedSlotIndex = slotIndex;

        // 吸附后：让双方计算重叠并应用加成
        card.OnSnappedToHost(host);
        host.OnChildAttached(card);
    }
}

