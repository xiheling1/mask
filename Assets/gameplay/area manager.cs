using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MaskOverlapManager : MonoBehaviour
{
    public static MaskOverlapManager Instance { get; private set; }

    private List<MaskOverlapZone> zones = new List<MaskOverlapZone>();

    // 吸附距离（世界/UI空间单位），超过则不吸附
    public float snapDistance = 80f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public class MaskOverlapZone : MonoBehaviour
    {
        // 吸附点（默认为 transform）
        public Transform snapPoint;

        // 区域内的卡片列表
        [HideInInspector]
        public List<MaskCardUI> cardsInZone = new List<MaskCardUI>();

        // 重叠触发阈值（0..1），可在 Inspector 调整
        [Range(0f, 1f)]
        public float overlapTrigger = 0.2f;

        // 容量，<=0 表示无限制
        public int capacity = 0;

        // 返回吸附位置（默认使用 transform）
        public Vector3 GetSnapPosition()
        {
            return snapPoint != null ? snapPoint.position : transform.position;
        }

        // 区域是否接受此卡片（可扩展为类型/标签判断）
        public bool CanAcceptCard(MaskCardUI card)
        {
            if (card == null) return false;
            if (capacity > 0 && cardsInZone.Count >= capacity) return false;
            return true;
        }

        public void RegisterCard(MaskCardUI card)
        {
            if (card == null) return;
            if (!cardsInZone.Contains(card)) cardsInZone.Add(card);
        }

        public void UnregisterCard(MaskCardUI card)
        {
            if (card == null) return;
            cardsInZone.Remove(card);
        }
    }

    public class MaskCardUI : MonoBehaviour
    {
        // 示例引用到你的 maskData 组件（可在 Inspector 赋值）
        public maskData mask;

        // 当前临时加成（示例用途）
        public int currentBonus = 0;

        // 可在此处添加更多 API，以配合 MaskOverlapManager 与 MaskOverlapZone
    }

    public void RegisterZone(MaskOverlapZone zone)
    {
        if (zone == null) return;

        // 避免重复注册
        if (!zones.Contains(zone))
        {
            zones.Add(zone);
            // 可在此处添加调试：Debug.Log($"注册区域: {zone.name}");
        }
    }

    public void UnregisterZone(MaskOverlapZone zone)
    {
        if (zone == null) return;

        if (zones.Contains(zone))
        {
            zones.Remove(zone);
            
        }
    }

    public MaskOverlapZone CheckSnap(Vector2 cardWorldPos, MaskCardUI card)
    {
        float closestDistance = float.MaxValue;
        MaskOverlapZone closestZone = null;

        // 遍历所有已注册区域
        foreach (var z in zones)
        {
            if (z == null) continue;

            // 步骤1：区域是否接受该卡片（类型、容量等规则）
            if (!z.CanAcceptCard(card)) continue;

            // 步骤2：计算距离（注意：确保坐标系一致）
            Vector2 zonePos2 = new Vector2(z.GetSnapPosition().x, z.GetSnapPosition().y);
            float distance = Vector2.Distance(cardWorldPos, zonePos2);

            // 步骤3：在吸附距离内并且比之前更近则记录为候选
            if (distance < snapDistance && distance < closestDistance)
            {
                closestDistance = distance;
                closestZone = z;
            }
        }

        return closestZone;
    }

    public List<MaskOverlapZone> GetAllZones()
    {
        return new List<MaskOverlapZone>(zones);
    }

    /// <summary>
    /// 清空所有已注册区域（场景切换或重置时使用）
    /// </summary>
    public void ClearAllZones()
    {
        zones.Clear();
    }
    public bool IsZoneRegistered(MaskOverlapZone zone)
    {
        return zones.Contains(zone);
    }
}

