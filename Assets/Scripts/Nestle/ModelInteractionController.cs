using UnityEngine;

public class ModelInteractionController : MonoBehaviour
{
    [Header("Rotation Axis Control")]
    public bool allowX = true;
    public bool allowY = true;
    public bool allowZ = false;

    [Header("Rotation Speed")]
    public float rotationSpeed = 5f;

    [Header("Clamp Control")]
    public bool useClamp = true;

    [Header("Per Axis Clamp Control")]
    public bool clampXEnabled = true;
    public bool clampYEnabled = true;
    public bool clampZEnabled = true;

    [Header("Rotation Clamp")]
    public Vector2 clampX = new Vector2(-180, 180);
    public Vector2 clampY = new Vector2(-180, 180);
    public Vector2 clampZ = new Vector2(-180, 180);

    [Header("Zoom (Scale-Based)")]
    public float zoomSpeed = 0.01f;
    public float minScale = 0.5f;
    public float maxScale = 2f;

    [Header("References")]
    public Camera targetCamera;

    [Header("Default State")]
    public Vector3 defaultRotation;

    private Vector3 currentRotation;
    private Vector3 initialScale;

    private bool isDragging = false;
    private Vector2 lastInputPos;

    void Start()
    {
        Vector3 euler = transform.eulerAngles;

        currentRotation.x = NormalizeAngle(euler.x);
        currentRotation.y = NormalizeAngle(euler.y);
        currentRotation.z = NormalizeAngle(euler.z);

        defaultRotation = currentRotation;
        initialScale = transform.localScale;
    }

    void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
        HandleMouse();
#elif UNITY_ANDROID || UNITY_IOS
        HandleTouch();
#endif
    }

    void HandleMouse()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverModel(Input.mousePosition))
            {
                isDragging = true;
                lastInputPos = Input.mousePosition;
                LookAround.isModelInteracting = true;
                return;
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            LookAround.isModelInteracting = false;
        }

        if (isDragging && Input.GetMouseButton(0))
        {
            Vector2 delta = (Vector2)Input.mousePosition - lastInputPos;
            lastInputPos = Input.mousePosition;

            RotateModel(delta);
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0 && IsPointerOverModel(Input.mousePosition))
        {
            LookAround.isModelInteracting = true;
            Zoom(scroll * 100f);
        }

        if (!Input.GetMouseButton(0) && scroll == 0)
        {
            LookAround.isModelInteracting = false;
        }
    }

    void HandleTouch()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began &&
                IsPointerOverModel(touch.position))
            {
                isDragging = true;
                lastInputPos = touch.position;
                LookAround.isModelInteracting = true;
                return;
            }
            else if (touch.phase == TouchPhase.Moved && isDragging)
            {
                RotateModel(touch.deltaPosition);
            }
            else if (touch.phase == TouchPhase.Ended)
            {
                isDragging = false;
                LookAround.isModelInteracting = false;
            }
        }

        if (Input.touchCount == 2)
        {
            LookAround.isModelInteracting = true;

            Touch t1 = Input.GetTouch(0);
            Touch t2 = Input.GetTouch(1);

            float prevDist = (t1.position - t1.deltaPosition -
                              (t2.position - t2.deltaPosition)).magnitude;

            float currDist = (t1.position - t2.position).magnitude;

            float delta = currDist - prevDist;

            Zoom(delta);
        }

        if (Input.touchCount == 0)
        {
            LookAround.isModelInteracting = false;
        }
    }

    void RotateModel(Vector2 delta)
    {
        float rotX = -delta.y * rotationSpeed * 0.02f;
        float rotY = -delta.x * rotationSpeed * 0.02f;

        if (allowX) currentRotation.x += rotX;
        if (allowY) currentRotation.y += rotY;
        if (allowZ) currentRotation.z += rotY;

        if (useClamp)
        {
            if (allowX && clampXEnabled)
                currentRotation.x = Mathf.Clamp(currentRotation.x, clampX.x, clampX.y);

            if (allowY && clampYEnabled)
                currentRotation.y = Mathf.Clamp(currentRotation.y, clampY.x, clampY.y);

            if (allowZ && clampZEnabled)
                currentRotation.z = Mathf.Clamp(currentRotation.z, clampZ.x, clampZ.y);
        }

        transform.rotation = Quaternion.Euler(currentRotation);
    }

    void Zoom(float delta)
    {
        float scaleChange = delta * zoomSpeed;

        Vector3 newScale = transform.localScale + Vector3.one * scaleChange;

        float clamped = Mathf.Clamp(newScale.x, minScale, maxScale);
        transform.localScale = new Vector3(clamped, clamped, clamped);
    }

    bool IsPointerOverModel(Vector2 screenPos)
    {
        if (targetCamera == null) return false;

        Ray ray = targetCamera.ScreenPointToRay(screenPos);
        return Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform;
    }

    public void ResetModel()
    {
        currentRotation = defaultRotation;
        transform.rotation = Quaternion.Euler(currentRotation);
        transform.localScale = initialScale;
    }

    float NormalizeAngle(float angle)
    {
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}