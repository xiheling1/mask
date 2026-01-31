using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 面具卡片（含槽位与重叠加成逻辑）
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class MaskCardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public static bool IsAnyCardDragging { get; private set; } = false;

    public maskData mask; // 面具数据引用

    // 可视化的槽位子对象（Inspector 可直接拖动子对象或在运行/编辑时生成）
    public Transform[] slotAnchors = new Transform[8];

    // 槽位图标预制（可在 Inspector 指定，若为空将用默认生成的小 Image）
    [Tooltip("可视化槽位图标预制（UI Image），为空时创建默认小圆点 Image")]
    public GameObject slotIconPrefab;

    // 是否显示槽位图标（创建后可切换）
    public bool showSlotIcons = true;

    // 槽位占用：若有卡驻留则为该卡引用，否则为 null
    private MaskCardUI[] slotOccupants = new MaskCardUI[8];

    // 如果此卡被吸附到其他卡，记录宿主和槽索引
    [HideInInspector] public MaskCardUI attachedHost;
    [HideInInspector] public int attachedSlotIndex = -1;

    // 重叠触发阈值（相对于较小者面积）
    [Range(0f, 1f)]
    public float overlapThreshold = 0.1f;

    // 当前临时加成（示例：累计的数值）
    public int currentBonus = 0;

    // 记录对每个相邻卡应用了多少加成（用于解除时回退）
    private Dictionary<MaskCardUI, int> appliedBonuses = new Dictionary<MaskCardUI, int>();

    RectTransform rect;
    Canvas parentCanvas;
    CanvasGroup canvasGroup;

    // Canvas 控制（用于拖拽时临时置顶）
    private Canvas selfCanvas;
    private bool originalOverrideSorting;
    private int originalSortingOrder;
    private Transform originalParent;

    // 默认方向（与管理器一致）——用于在没有 slotAnchors 时计算默认位置
    private static readonly Vector3[] defaultDirs = new Vector3[]
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
        rect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        parentCanvas = GetComponentInParent<Canvas>();

        selfCanvas = GetComponent<Canvas>();
        if (selfCanvas == null)
        {
            // 只添加 Canvas（用于 overrideSorting），但不要同时添加 GraphicRaycaster 到每个预制件上
            selfCanvas = gameObject.AddComponent<Canvas>();
            selfCanvas.overrideSorting = false;
        }

        originalOverrideSorting = selfCanvas.overrideSorting;
        originalSortingOrder = selfCanvas.sortingOrder;
        originalParent = transform.parent;
    }

    void OnEnable()
    {
        MaskOverlapManager.Instance?.RegisterMask(this);
        if (showSlotIcons) EnsureSlotIcons();
    }

    void OnDisable()
    {
        // 若自己是某宿主的槽位，先清理宿主记录（并移除与宿主间的加成）
        if (attachedHost != null)
        {
            attachedHost.ClearSlot(attachedSlotIndex);
            attachedHost = null;
            attachedSlotIndex = -1;
        }
        MaskOverlapManager.Instance?.UnregisterMask(this);

        // 当自身被禁用/销毁，移除所有 appliedBonuses 对别人的影响（如果需要）
        foreach (var kv in new List<MaskCardUI>(appliedBonuses.Keys))
        {
            RemoveOverlapBonus(kv);
        }

        // 确保拖拽标志在销毁/禁用时被清理
        if (IsAnyCardDragging)
        {
            IsAnyCardDragging = false;
        }
    }

    #region 槽位管理（供管理器调用）

    public bool IsSlotFree(int index)
    {
        if (index < 0 || index >= slotOccupants.Length) return false;
        return slotOccupants[index] == null;
    }

    public void SetSlot(int index, MaskCardUI card)
    {
        if (index < 0 || index >= slotOccupants.Length) return;
        slotOccupants[index] = card;
    }

    public void ClearSlot(int index)
    {
        if (index < 0 || index >= slotOccupants.Length) return;
        MaskCardUI occ = slotOccupants[index];
        if (occ != null)
        {
            // 解除与该占位卡的重叠加成（双方）
            RemoveOverlapBonus(occ);
            occ.RemoveOverlapBonus(this);
        }
        slotOccupants[index] = null;
    }

    #endregion

    #region 槽位工具（编辑与运行时辅助）

    /// <summary>
    /// 返回槽位的世界坐标：如果 slotAnchors[index] 存在则使用其位置，
    /// 否则根据默认方向和管理器的 slotDistance 计算位置（在 UI 情况下保证 transform.TransformPoint 可用）。
    /// </summary>
    public Vector3 GetSlotWorldPosition(int index)
    {
        if (index < 0 || index >= slotAnchors.Length) return transform.position;
        if (slotAnchors[index] != null) return slotAnchors[index].position;

        float distance = MaskOverlapManager.Instance != null ? MaskOverlapManager.Instance.slotDistance : 60f;
        Vector3 localOffset = defaultDirs[index] * distance;
        return transform.TransformPoint(localOffset);
    }

    /// <summary>
    /// 在 Inspector 的上下文菜单中调用：为当前对象生成 8 个默认槽位子对象（不会覆盖已存在的 slotAnchors）
    /// 并会根据 showSlotIcons 自动创建图标
    /// </summary>
    [ContextMenu("Create Default Slots")]
    public void CreateDefaultSlots()
    {
        float distance = MaskOverlapManager.Instance != null ? MaskOverlapManager.Instance.slotDistance : 60f;

        for (int i = 0; i < 8; i++)
        {
            if (slotAnchors[i] != null) continue;

            GameObject go = new GameObject($"Slot {i}");
            // 若父对象是 UI（RectTransform），则创建 RectTransform 子对象以便对齐
            RectTransform parentRect = GetComponent<RectTransform>();
            if (parentRect != null)
            {
                RectTransform rt = go.AddComponent<RectTransform>();
                rt.SetParent(parentRect, worldPositionStays: false);
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one;
                rt.localPosition = defaultDirs[i] * distance;
            }
            else
            {
                go.transform.SetParent(transform, worldPositionStays: false);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                go.transform.localPosition = defaultDirs[i] * distance;
            }
            slotAnchors[i] = go.transform;

            // 创建图标（如果需要）
            if (showSlotIcons) CreateSlotIcon(slotAnchors[i]);
        }
    }

    /// <summary>
    /// 删除所有自动生成的槽位（仅删除名为 Slot 0..7 的子对象并清空对应引用）
    /// </summary>
    [ContextMenu("Clear Auto Slots")]
    public void ClearAutoSlots()
    {
        for (int i = 0; i < 8; i++)
        {
            if (slotAnchors[i] != null && slotAnchors[i].name.StartsWith("Slot "))
            {
                // 删除图标（子对象名 Icon）
                Transform icon = slotAnchors[i].Find("Icon");
                if (icon != null)
                {
#if UNITY_EDITOR
                    UnityEngine.Object.DestroyImmediate(icon.gameObject);
#else
                    Destroy(icon.gameObject);
#endif
                }

#if UNITY_EDITOR
                UnityEngine.Object.DestroyImmediate(slotAnchors[i].gameObject);
#else
                Destroy(slotAnchors[i].gameObject);
#endif
                slotAnchors[i] = null;
            }
        }
    }

    /// <summary>
    /// 为指定 slot 创建图标（UI 情况创建 Image，非 UI 创建 SpriteRenderer）
    /// </summary>
    void CreateSlotIcon(Transform slot)
    {
        if (slot == null) return;

        // 如果已经有 Icon 子对象，跳过
        Transform exist = slot.Find("Icon");
        if (exist != null) return;

        RectTransform parentRect = GetComponent<RectTransform>();
        if (parentRect != null)
        {
            GameObject iconGo;
            if (slotIconPrefab != null)
            {
                iconGo = Instantiate(slotIconPrefab, slot, worldPositionStays: false);
                iconGo.name = "Icon";
            }
            else
            {
                iconGo = new GameObject("Icon");
                RectTransform irt = iconGo.AddComponent<RectTransform>();
                irt.SetParent(slot, worldPositionStays: false);
                irt.localScale = Vector3.one;
                irt.localRotation = Quaternion.identity;
                irt.anchoredPosition = Vector2.zero;
                irt.sizeDelta = new Vector2(16, 16);
                Image img = iconGo.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0.8f);
            }
        }
        else
        {
            // 非 UI：使用 SpriteRenderer（需要自备 Sprite）
            if (slotIconPrefab != null)
            {
                GameObject iconGo = Instantiate(slotIconPrefab, slot, worldPositionStays: false);
                iconGo.name = "Icon";
            }
            else
            {
                GameObject iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(slot, worldPositionStays: false);
                iconGo.transform.localScale = Vector3.one;
                iconGo.transform.localRotation = Quaternion.identity;
                iconGo.transform.localPosition = Vector3.zero;
                SpriteRenderer sr = iconGo.AddComponent<SpriteRenderer>();
                sr.color = new Color(1f, 1f, 1f, 0.8f);
            }
        }
    }

    /// <summary>
    /// 为已有的 slotAnchors 确保图标存在（在 OnEnable 或切换 showSlotIcons 时调用）
    /// </summary>
    public void EnsureSlotIcons()
    {
        for (int i = 0; i < slotAnchors.Length; i++)
        {
            if (slotAnchors[i] != null)
            {
                CreateSlotIcon(slotAnchors[i]);
            }
        }
    }

    /// <summary>
    /// 删除所有槽位图标（不删除槽位 Transform 本身）
    /// </summary>
    public void ClearAllSlotIcons()
    {
        for (int i = 0; i < slotAnchors.Length; i++)
        {
            if (slotAnchors[i] == null) continue;
            Transform icon = slotAnchors[i].Find("Icon");
            if (icon != null)
            {
#if UNITY_EDITOR
                UnityEngine.Object.DestroyImmediate(icon.gameObject);
#else
                Destroy(icon.gameObject);
#endif
            }
        }
    }

    #endregion

    #region 拖拽交互（UI）

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 标记正在拖拽卡片，通知相机不要响应拖拽
        IsAnyCardDragging = true;

        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
        // 放下时临时从宿主解绑（先清理宿主的 slot 记录）
        if (attachedHost != null)
        {
            attachedHost.ClearSlot(attachedSlotIndex);
            attachedHost = null;
            attachedSlotIndex = -1;
            // 取消父对象，保留世界位置
            transform.SetParent(parentCanvas != null ? parentCanvas.transform : null, worldPositionStays: true);
        }

        // 临时将该对象提升到 UI 顶层：先把父对象设置为根 Canvas（已在上面），然后 SetAsLastSibling
        transform.SetAsLastSibling();

        // 关键：如果被其他 Canvas 覆盖，临时 override sorting 并提高 sortingOrder
        if (selfCanvas != null)
        {
            selfCanvas.overrideSorting = true;
            selfCanvas.sortingOrder = 1000;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (parentCanvas == null) return;
        Vector3 worldPoint;
        Camera cam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parentCanvas.transform as RectTransform, eventData.position, cam, out worldPoint))
        {
            rect.position = worldPoint;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 清除拖拽标志
        IsAnyCardDragging = false;

        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;

        Vector2 worldPos = rect.position;
        if (MaskOverlapManager.Instance != null &&
            MaskOverlapManager.Instance.FindNearestFreeSlot(worldPos, this, out MaskCardUI host, out int slotIndex, out Vector3 slotWorldPos))
        {
            MaskOverlapManager.Instance.SnapToSlot(host, slotIndex, this);
        }
        else
        {
            // 未吸附：可回退到原位（按需实现）
        }

        // 恢复 Canvas 排序设置
        if (selfCanvas != null)
        {
            selfCanvas.overrideSorting = originalOverrideSorting;
            selfCanvas.sortingOrder = originalSortingOrder;
        }
    }

    #endregion

    #region 重叠计算与加成

    // 当被吸附到宿主时调用：计算与宿主的重叠并应用加成
    public void OnSnappedToHost(MaskCardUI host)
    {
        if (host == null) return;

        float overlap = ComputeOverlapPercent(this.rect, host.rectTransformSafe());
        if (overlap >= overlapThreshold)
        {
            ApplyOverlapBonus(host, overlap);
            host.ApplyOverlapBonus(this, overlap);
        }
    }

    // 当宿主接到子卡时也会调用（两边都触发以确保记录完整）
    public void OnChildAttached(MaskCardUI child)
    {
        if (child == null) return;
        float overlap = ComputeOverlapPercent(this.rect, child.rectTransformSafe());
        if (overlap >= overlapThreshold)
        {
            ApplyOverlapBonus(child, overlap);
            child.ApplyOverlapBonus(this, overlap);
        }
    }

    // 计算两个 RectTransform 在屏幕空间的重叠比例（相对于较小者面积）
    private float ComputeOverlapPercent(RectTransform a, RectTransform b)
    {
        if (a == null || b == null) return 0f;

        Canvas canvas = parentCanvas;
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        Vector3[] ac = new Vector3[4];
        Vector3[] bc = new Vector3[4];
        a.GetWorldCorners(ac);
        b.GetWorldCorners(bc);

        Vector2 aMin = RectTransformUtility.WorldToScreenPoint(cam, ac[0]);
        Vector2 aMax = RectTransformUtility.WorldToScreenPoint(cam, ac[2]);
        Vector2 bMin = RectTransformUtility.WorldToScreenPoint(cam, bc[0]);
        Vector2 bMax = RectTransformUtility.WorldToScreenPoint(cam, bc[2]);

        Rect ra = new Rect(aMin.x, aMin.y, aMax.x - aMin.x, aMax.y - aMin.y);
        Rect rb = new Rect(bMin.x, bMin.y, bMax.x - bMin.x, bMax.y - bMin.y);

        if (!ra.Overlaps(rb)) return 0f;

        float xMin = Mathf.Max(ra.xMin, rb.xMin);
        float xMax = Mathf.Min(ra.xMax, rb.xMax);
        float yMin = Mathf.Max(ra.yMin, rb.yMin);
        float yMax = Mathf.Min(ra.yMax, rb.yMax);

        float interArea = Mathf.Max(0, xMax - xMin) * Mathf.Max(0, yMax - yMin);
        float areaA = ra.width * ra.height;
        float areaB = rb.width * rb.height;
        float smaller = Mathf.Min(areaA, areaB);
        if (smaller <= 0f) return 0f;
        return interArea / smaller;
    }

    // 应用重叠加成：计算并记录（避免重复）
    public void ApplyOverlapBonus(MaskCardUI other, float overlapPercent)
    {
        if (other == null) return;
        if (appliedBonuses.ContainsKey(other)) return; // 已经应用过

        int myEffect = mask != null ? mask.Effect : 0;
        int otherEffect = other.mask != null ? other.mask.Effect : 0;
        int bonus = Mathf.RoundToInt((myEffect + otherEffect) * overlapPercent);

        if (bonus != 0)
        {
            appliedBonuses[other] = bonus;
            currentBonus += bonus;
            // 可在此触发事件或更新 UI，示例：
            // Debug.Log($"{name} 从重叠获得加成 {bonus}（与 {other.name}）");
        }
    }

    // 移除之前对 other 应用的加成（用于解绑或销毁）
    public void RemoveOverlapBonus(MaskCardUI other)
    {
        if (other == null) return;
        if (appliedBonuses.TryGetValue(other, out int bonus))
        {
            currentBonus -= bonus;
            appliedBonuses.Remove(other);
            // Debug.Log($"{name} 移除与 {other.name} 的重叠加成 {bonus}");
        }
    }

    // 辅助：确保拿到有效的 RectTransform（有时对象不是 UI）
    private RectTransform rectTransformSafe()
    {
        if (rect != null) return rect;
        return GetComponent<RectTransform>();
    }

    #endregion
}