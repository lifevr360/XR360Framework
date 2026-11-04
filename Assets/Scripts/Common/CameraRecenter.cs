using UnityEngine;

// Turns the view back to the scene's forward direction.
// Quest: rotates the XR Origin so wherever the user is looking becomes "forward".
// Mobile: resets the Mouselook camera to its start rotation.
// No XR package needed, so the same script works on the VR and the Android branch.
public class CameraRecenter : MonoBehaviour
{
    [Tooltip("The camera. Empty = Main Camera")]
    public Transform head;
    [Tooltip("Quest: the XR Origin (XR Rig). Empty = found from the camera (camera > Camera Offset > XR Origin)")]
    public Transform rig;
    [Tooltip("Mobile: the Mouselook on the camera. Empty = found on the camera or its parents")]
    public Mouselook mouseLook;
    [Tooltip("Quest: also move the user back to where the rig started (on the floor plane)")]
    public bool recenterPosition = true;

    private float forwardYaw;
    private Vector3 startPosition;
    private Quaternion mouseLookStartRotation;
    [Tooltip("Quest: after head tracking starts, wait this long (seconds) before a recenter asked for at startup")]
    public float trackingSettleTime = 0.5f;

    private bool ready;

    // Quest: at startup the headset has no pose yet, so an early recenter waits for tracking
    private Vector3 untrackedHeadPosition;
    private Quaternion untrackedHeadRotation;
    private bool tracking;
    private float trackingStartTime;
    private bool pendingRecenter;

    private void Awake()
    {
        if (head == null && Camera.main != null)
            head = Camera.main.transform;
        if (head == null)
        {
            Debug.LogError("CameraRecenter: no camera found, recentering is off.");
            return;
        }

        if (mouseLook == null)
            mouseLook = head.GetComponentInParent<Mouselook>();

        // Mobile uses Mouselook; only look for an XR rig when there is none
        if (rig == null && mouseLook == null && head.parent != null)
            rig = head.parent.parent != null ? head.parent.parent : head.parent;

        if (rig != null)
        {
            forwardYaw = rig.eulerAngles.y;
            startPosition = rig.position;
        }
        if (mouseLook != null)
            mouseLookStartRotation = mouseLook.transform.localRotation;

        untrackedHeadPosition = head.localPosition;
        untrackedHeadRotation = head.localRotation;

        ready = true;
    }

    private void Update()
    {
        if (!ready || rig == null || mouseLook != null)
            return;

        // Tracking has started once the headset moves the camera away from its scene pose
        if (!tracking && (head.localPosition != untrackedHeadPosition || head.localRotation != untrackedHeadRotation))
        {
            tracking = true;
            trackingStartTime = Time.time;
        }

        if (pendingRecenter && tracking && Time.time - trackingStartTime >= trackingSettleTime)
        {
            pendingRecenter = false;
            Recenter();
        }
    }

    public void Recenter()
    {
        if (!ready)
            return;

        if (mouseLook != null)
        {
            mouseLook.ResetRotation(mouseLookStartRotation);
            return;
        }

        if (rig == null)
            return;

        // Too early (e.g. the first image on app start): do it once the headset pose has settled
        if (!tracking || Time.time - trackingStartTime < trackingSettleTime)
        {
            pendingRecenter = true;
            return;
        }

        // Turn the rig around the head so the head faces the scene's forward again
        float turn = Mathf.DeltaAngle(head.eulerAngles.y, forwardYaw);
        rig.RotateAround(head.position, Vector3.up, turn);

        if (recenterPosition)
        {
            Vector3 offset = startPosition - head.position;
            offset.y = 0;
            rig.position += offset;
        }
    }
}
