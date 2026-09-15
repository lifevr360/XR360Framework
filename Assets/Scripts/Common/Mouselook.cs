using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Camera-Control/Advanced 360 Look")]
public class Mouselook : MonoBehaviour
{
    public enum RotationAxes { MouseXAndY = 0, MouseX = 1, MouseY = 2 }

    [Header("Axes")]
    public RotationAxes axes = RotationAxes.MouseXAndY;

    [Header("Desktop Sensitivity")]
    public float sensitivityX = 1.0f;
    public float sensitivityY = 0.5f;

    [Header("Mobile/Touch Sensitivity")]
    public float mobileSensitivityX = 2.5f; 
    public float mobileSensitivityY = 1.5f; 

    [Header("Vertical Clamp")]
    public float minimumY = -85f;
    public float maximumY = 85f;

    [Header("Smoothness")]
    public float smoothSpeed = 12f;
    public float momentumDamping = 3f;

    [Header("Gyroscope")]
    public bool enableGyro = true;
    public float gyroSensitivity = 2.0f;
    public Button permissionButton; 

    private float rotationX;
    private float rotationY;
    private float targetRotationX;
    private float targetRotationY;
    private float velocityX;
    private float velocityY;

    void Start()
    {
        InitializeRotations();

        if (permissionButton != null)
        {
            permissionButton.onClick.AddListener(RequestGyroPermission);
        }
        
        if (enableGyro)
        {
            // Note: On many mobile browsers, this requires a user gesture (like our overlay click)
            Input.gyro.enabled = true;
        }
    }

    private void InitializeRotations()
    {
        Vector3 rot = transform.localEulerAngles;
        targetRotationX = rotationX = rot.y;
        float x = rot.x;
        if (x > 180) x -= 360;
        targetRotationY = rotationY = x;
    }

    public void RequestGyroPermission()
    {
        Input.gyro.enabled = true;
        if (permissionButton != null) permissionButton.gameObject.SetActive(false);
    }

    public void ResetRotation(Quaternion newRotation)
    {
        Vector3 angles = newRotation.eulerAngles;
        float x = angles.x;
        if (x > 180) x -= 360;

        rotationX = targetRotationX = angles.y;
        rotationY = targetRotationY = x;

        velocityX = 0;
        velocityY = 0;

        transform.localRotation = Quaternion.Euler(rotationY, rotationX, 0);
    }

    void Update()
    {
        bool interacted = false;

        if (Input.touchCount > 0)
        {
            interacted = HandleTouch();
        }
        else if (Input.GetMouseButton(0))
        {
            interacted = HandleMouse();
        }

        if (!interacted)
        {
            if (enableGyro && Input.gyro.enabled) ApplyGyro();
            ApplyMomentum();
        }

        ApplyRotation();
    }

    bool HandleMouse()
    {
        velocityX = Input.GetAxis("Mouse X") * sensitivityX * 10f;
        velocityY = Input.GetAxis("Mouse Y") * sensitivityY * 10f;

        UpdateTargetRotations(velocityX, velocityY);
        return true;
    }

    bool HandleTouch()
    {
        Touch touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Moved)
        {
            float deltaX = (touch.deltaPosition.x / Screen.width) * mobileSensitivityX * 100f;
            float deltaY = (touch.deltaPosition.y / Screen.height) * mobileSensitivityY * 100f;

            UpdateTargetRotations(deltaX, deltaY);
            return true;
        }
        return touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Stationary;
    }

    void ApplyGyro()
    {
        Vector3 gyroRate = Input.gyro.rotationRateUnbiased;
        if (gyroRate.magnitude > 0.01f)
        {
            UpdateTargetRotations(-gyroRate.y * gyroSensitivity, -gyroRate.x * gyroSensitivity);
        }
    }

    void UpdateTargetRotations(float xDelta, float yDelta)
    {
        targetRotationX += xDelta;
        targetRotationY -= yDelta;
        targetRotationY = Mathf.Clamp(targetRotationY, minimumY, maximumY);
    }

    void ApplyMomentum()
    {
        velocityX = Mathf.Lerp(velocityX, 0, Time.deltaTime * momentumDamping);
        velocityY = Mathf.Lerp(velocityY, 0, Time.deltaTime * momentumDamping);
        UpdateTargetRotations(velocityX, velocityY);
    }

    void ApplyRotation()
    {
        rotationX = Mathf.LerpAngle(rotationX, targetRotationX, Time.deltaTime * smoothSpeed);
        rotationY = Mathf.Lerp(rotationY, targetRotationY, Time.deltaTime * smoothSpeed);

        transform.localRotation = Quaternion.Euler(
            (axes != RotationAxes.MouseX) ? rotationY : 0,
            (axes != RotationAxes.MouseY) ? rotationX : 0,
            0
        );
    }
}