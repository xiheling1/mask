using UnityEngine;
using UnityEditor;

// 编辑器扩展：在 Scene 视图显示可拖拽的把手来移动 slotAnchors（支持 Undo）
[CustomEditor(typeof(MaskCardUI))]
public class MaskCardUIEditor : Editor
{
    void OnSceneGUI()
    {
        MaskCardUI t = (MaskCardUI)target;
        if (t == null) return;

        // 绘制并允许拖拽每个槽位把手
        for (int i = 0; i < 8; i++)
        {
            // 获取当前世界位置（若无 slotAnchors 则返回默认计算位置）
            Vector3 worldPos = t.GetSlotWorldPosition(i);

            // 颜色和标签
            Handles.color = Color.cyan;
            Vector3 newWorldPos = Handles.PositionHandle(worldPos, Quaternion.identity);

            if (newWorldPos != worldPos)
            {
                // 发生更改：记录 Undo，并把 slotAnchors[i] 的 transform 更新到 newWorldPos
                Undo.RecordObject(t, "Move Slot Anchor");

                // 如果 slotAnchors[i] 为 null，则创建新的 Slot i（RectTransform if UI）
                if (t.slotAnchors[i] == null)
                {
                    GameObject go = new GameObject($"Slot {i}");
                    RectTransform parentRect = t.GetComponent<RectTransform>();
                    if (parentRect != null)
                    {
                        RectTransform rt = go.AddComponent<RectTransform>();
                        rt.SetParent(parentRect, worldPositionStays: true);
                        // 将世界位置设置为 newWorldPos
                        rt.position = newWorldPos;
                    }
                    else
                    {
                        go.transform.SetParent(t.transform, worldPositionStays: true);
                        go.transform.position = newWorldPos;
                    }

                    // mark undo for creation
                    Undo.RegisterCreatedObjectUndo(go, "Create Slot Anchor");
                    t.slotAnchors[i] = go.transform;
                }
                else
                {
                    // 直接移动已有 slotAnchors[i]
                    Transform anchor = t.slotAnchors[i];
                    // 对 RectTransform 做特殊处理以保持 UI 对齐
                    RectTransform rt = anchor as RectTransform;
                    if (rt != null)
                    {
                        // 将世界位置设置为 newWorldPos（保留父关系）
                        rt.position = newWorldPos;
                    }
                    else
                    {
                        anchor.position = newWorldPos;
                    }
                }

                // 如果 showSlotIcons 为 true，确保图标存在
                if (t.showSlotIcons)
                {
                    t.EnsureSlotIcons();
                }

                EditorUtility.SetDirty(t);
            }

            // Label
            Handles.Label(worldPos + Vector3.up * 12f, $"Slot {i}");
        }
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MaskCardUI t = (MaskCardUI)target;
        GUILayout.Space(6);

        if (GUILayout.Button("Create Default Slots"))
        {
            Undo.RecordObject(t, "Create Default Slots");
            t.CreateDefaultSlots();
            EditorUtility.SetDirty(t);
        }

        if (GUILayout.Button("Clear Auto Slots"))
        {
            Undo.RecordObject(t, "Clear Auto Slots");
            t.ClearAutoSlots();
            EditorUtility.SetDirty(t);
        }

        if (GUILayout.Button(t.showSlotIcons ? "Hide Slot Icons" : "Show Slot Icons"))
        {
            Undo.RecordObject(t, "Toggle Slot Icons");
            t.showSlotIcons = !t.showSlotIcons;
            if (t.showSlotIcons) t.EnsureSlotIcons();
            else t.ClearAllSlotIcons();
            EditorUtility.SetDirty(t);
        }
    }
}