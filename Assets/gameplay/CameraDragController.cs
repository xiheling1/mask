using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 摄像机拖拽与缩放控制
/// - 鼠标：按住 Space 或中键（Middle）拖动平移；滚轮缩放
/// - 触摸：单指拖动平移，双指捏合缩放
/// - 会自动忽略当鼠标/触摸在 UI 上时（EventSystem）或正在拖拽卡片时（MaskCardUI.IsAnyCardDragging）
/// - 支持可选边界限制（panBounds）
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraDragController : MonoBehaviour
{
    public Camera targetCamera;

    [Header("Pan Settings")]
    public bool panWithMiddleMouse = true;
    public bool panWithRightMouse = false;
    public bool panWithSpace = true;
    public float panSpeed = 1f;
    public Rect panBounds = new Rect(-1000, -1000, 2000, 2000); // world space bounds, optional

    [Header("Zoom Settings")]
    public float zoomSpeed = 1f;
    public float minOrthoSize = 2f;
    public float maxOrthoSize = 100f;
    public bool clampToBounds = true;

    // 内部状态
    private Vector3 lastPanPosition;
    private int panFingerId = -1;
    private bool isPanning;

    void Awake()
    {
        if (targetCamera == null) targetCamera = GetComponent<Camera>();
        if (targetCamera == null) Debug.LogError("CameraDragController: targetCamera 未设置也未找到 Camera 组件。");
    }

    void Update()
    {
        // 如果没有 EventSystem 则继续（防空）
        bool pointerOverUI = EventSystem.current != null && IsPointerOverUI();

        // 如果 UI 正在交互或卡片正在拖拽，则不响应相机拖动
        if (pointerOverUI || MaskCardUI.IsAnyCardDragging) return;

        #if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouse();
        #endif

        HandleTouch();
    }

    bool IsPointerOverUI()
    {
        // 鼠标或当前触摸是否在 UI 上
        if (EventSystem.current == null) return false;
        // 鼠标
        if (Input.mousePresent)
        {
            if (EventSystem.current.IsPointerOverGameObject()) return true;
        }
        // 触摸
        for (int i = 0; i < Input.touchCount; i++)
        {
            if (EventSystem.current.IsPointerOverGameObject(Input.touches[i].fingerId)) return true;
        }
        return false;
    }

    void HandleMouse()
    {
        // 缩放（滚轮）
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            Zoom(scroll * zoomSpeed * 100f);
        }

        bool panKey = (panWithSpace && Input.GetKey(KeyCode.Space));
        bool startPan = (panWithMiddleMouse && Input.GetMouseButtonDown(2))
                        || (panWithRightMouse && Input.GetMouseButtonDown(1))
                        || (panWithSpace && Input.GetMouseButtonDown(0) && panKey);

        // 开始平移（按下对应按钮）
        if (startPan)
        {
            isPanning = true;
            lastPanPosition = Input.mousePosition;
        }

        // 结束平移
        if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2))
        {
            isPanning = false;
        }

        // 执行平移
        if (isPanning)
        {
            Vector3 newPanPosition = Input.mousePosition;
            Vector3 delta = targetCamera.ScreenToWorldPoint(lastPanPosition) - targetCamera.ScreenToWorldPoint(newPanPosition);
            Pan(delta * panSpeed);
            lastPanPosition = newPanPosition;
        }
    }

    void HandleTouch()
    {
        if (Input.touchCount == 0) return;

        if (Input.touchCount == 1)
        {
            Touch t = Input.GetTouch(0);
            // 防止在开始触摸时马上被 UI 拦截（上面已使用 IsPointerOverUI）
            if (t.phase == TouchPhase.Began)
            {
                panFingerId = t.fingerId;
                lastPanPosition = t.position;
                isPanning = true;
            }
            else if (t.phase == TouchPhase.Moved && isPanning && t.fingerId == panFingerId)
            {
                Vector3 delta = targetCamera.ScreenToWorldPoint(lastPanPosition) - targetCamera.ScreenToWorldPoint(t.position);
                Pan(delta * panSpeed);
                lastPanPosition = t.position;
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                if (t.fingerId == panFingerId) panFingerId = -1;
                isPanning = false;
            }
        }
        else if (Input.touchCount >= 2)
        {
            // 双指捏合缩放
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            if (t0.phase == TouchPhase.Moved || t1.phase == TouchPhase.Moved)
            {
                Vector2 prevPos0 = t0.position - t0.deltaPosition;
                Vector2 prevPos1 = t1.position - t1.deltaPosition;

                float prevMag = (prevPos0 - prevPos1).magnitude;
                float curMag = (t0.position - t1.position).magnitude;
                float diff = curMag - prevMag;

                Zoom(-diff * 0.01f * zoomSpeed * 10f);
            }
        }
    }

    void Pan(Vector3 worldDelta)
    {
        if (targetCamera.orthographic)
        {
            Vector3 newPos = targetCamera.transform.position + worldDelta;
            if (clampToBounds)
            {
                newPos = ClampToBounds(newPos);
            }
            targetCamera.transform.position = newPos;
        }
        else
        {
            // 对透视相机，简单平移相机的位置
            Vector3 newPos = targetCamera.transform.position + worldDelta;
            if (clampToBounds)
            {
                newPos = ClampToBounds(newPos);
            }
            targetCamera.transform.position = newPos;
        }
    }

    void Zoom(float delta)
    {
        if (!targetCamera.orthographic)
        {
            // 对透视相机使用移动前后距离模拟缩放（简单实现）
            targetCamera.transform.Translate(Vector3.forward * delta, Space.Self);
            return;
        }

        float size = targetCamera.orthographicSize;
        size -= delta;
        size = Mathf.Clamp(size, minOrthoSize, maxOrthoSize);
        targetCamera.orthographicSize = size;

        if (clampToBounds)
        {
            Vector3 clamped = ClampToBounds(targetCamera.transform.position);
            targetCamera.transform.position = clamped;
        }
    }

    Vector3 ClampToBounds(Vector3 camPos)
    {
        // 计算相机视口在世界坐标中的宽高（仅对正交有效）
        if (targetCamera.orthographic)
        {
            float vert = targetCamera.orthographicSize;
            float hor = vert * targetCamera.aspect;
            float left = camPos.x - hor;
            float right = camPos.x + hor;
            float bottom = camPos.y - vert;
            float top = camPos.y + vert;

            float minX = panBounds.xMin + hor;
            float maxX = panBounds.xMax - hor;
            float minY = panBounds.yMin + vert;
            float maxY = panBounds.yMax - vert;

            camPos.x = Mathf.Clamp(camPos.x, minX, maxX);
            camPos.y = Mathf.Clamp(camPos.y, minY, maxY);
        }
        else
        {
            camPos.x = Mathf.Clamp(camPos.x, panBounds.xMin, panBounds.xMax);
            camPos.y = Mathf.Clamp(camPos.y, panBounds.yMin, panBounds.yMax);
            camPos.z = Mathf.Clamp(camPos.z, -1000f, 1000f);
        }

        return camPos;
    }
}