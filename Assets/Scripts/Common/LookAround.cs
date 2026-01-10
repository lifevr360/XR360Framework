using UnityEngine;

public class LookAround : MonoBehaviour
{
    [Header("Sensitivity Settings")]
    public float sensitivity = 12f;

    [Header("Rotation Limits")]
    public float minX = -360f; // Min horizontal rotation
    public float maxX = 360f;  // Max horizontal rotation
    public float minY = -60f; // Min vertical rotation
    public float maxY = 60f;  // Max vertical rotation

    private float rotationX = 0f;
    private float rotationY = 0f;

    private Vector2 lastMousePosition;
    private Vector2 lastTouchPosition;
    private bool isTouching = false;

    void Update()
    {
#if UNITY_WEBGL || UNITY_STANDALONE || UNITY_EDITOR
        HandleMouseLook();
#elif UNITY_ANDROID || UNITY_IOS
        HandleTouchLook();
#endif
    }

    void HandleMouseLook()
    {
        if (Input.GetMouseButtonDown(0)) // Left click pressed
        {
            lastMousePosition = Input.mousePosition;
        }
        else if (Input.GetMouseButton(0)) // Left click held
        {
            Vector2 mouseDelta = (Vector2)Input.mousePosition - lastMousePosition;
            lastMousePosition = Input.mousePosition;

            float mouseX = mouseDelta.x * sensitivity * 0.01f; // Adjust for smooth movement
            float mouseY = mouseDelta.y * sensitivity * 0.01f;

            rotationX += mouseX;
            rotationY -= mouseY;

            rotationX = Mathf.Clamp(rotationX, minX, maxX);
            rotationY = Mathf.Clamp(rotationY, minY, maxY);

            transform.rotation = Quaternion.Euler(rotationY, rotationX, 0f);
        }
    }

    void HandleTouchLook()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                lastTouchPosition = touch.position;
                isTouching = true;
            }
            else if (touch.phase == TouchPhase.Moved && isTouching)
            {
                Vector2 delta = touch.deltaPosition;

                float touchX = delta.x * sensitivity * 0.01f;
                float touchY = delta.y * sensitivity * 0.01f;

                rotationX += touchX;
                rotationY -= touchY;

                rotationX = Mathf.Clamp(rotationX, minX, maxX);
                rotationY = Mathf.Clamp(rotationY, minY, maxY);

                transform.rotation = Quaternion.Euler(rotationY, rotationX, 0f);
            }
            else if (touch.phase == TouchPhase.Ended)
            {
                isTouching = false;
            }
        }
    }
}
