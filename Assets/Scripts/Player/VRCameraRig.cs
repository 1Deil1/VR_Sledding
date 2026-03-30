using UnityEngine;

/// <summary>
/// Attaches the VR camera rig (XR Origin) to the sled so the player's view
/// rides along with the sled position.
///
/// The body forward direction follows the sled, but head rotation is left
/// entirely to the XR device — do NOT override it here.
/// Attach this to the XR Origin GameObject and drag the Sled transform in.
/// </summary>
public class VRCameraRig : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform sledTransform;

    [Header("Camera Position Offset")]
    [Tooltip("Height and position of the camera origin relative to the sled pivot.")]
    [SerializeField] private Vector3 seatOffset = new Vector3(0f, 0.5f, 0f);

    [Header("Body Rotation Speed")]
    [Tooltip("How quickly the body-forward direction catches up with the sled's heading.")]
    [SerializeField, Range(1f, 20f)] private float bodyRotationSpeed = 5f;

    [Header("View Distance")]
    [Tooltip("Camera far clip plane in metres. Increase for a longer view down the slope.")]
    [SerializeField] private float farClipPlane = 800f;

    private void Start()
    {
        // Apply far clip to every camera under this rig (covers both eyes in VR).
        foreach (Camera cam in GetComponentsInChildren<Camera>(true))
            cam.farClipPlane = farClipPlane;

        // Ensure exactly one AudioListener exists in the scene.
        // The XR camera already has one; destroy any others (e.g. the default Main Camera).
        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        AudioListener keepListener = GetComponentInChildren<AudioListener>(true);

        // If none is under this rig, keep the first one found and remove the rest.
        if (keepListener == null && listeners.Length > 0)
            keepListener = listeners[0];

        foreach (AudioListener al in listeners)
        {
            if (al != keepListener)
            {
                Debug.Log($"[VRCameraRig] Removing duplicate AudioListener from '{al.gameObject.name}'.");
                Destroy(al);
            }
        }
    }

    private void LateUpdate()
    {
        if (sledTransform == null) return;

        // Lock camera rig origin to sled seat position
        transform.position = sledTransform.position + seatOffset;

        // Rotate the rig so the body faces the sled's forward direction.
        // The XR system adds head-tracking on top of this automatically.
        Vector3 flatForward = Vector3.ProjectOnPlane(sledTransform.forward, Vector3.up);
        if (flatForward.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(flatForward, Vector3.up);
            transform.rotation   = Quaternion.Slerp(transform.rotation, targetRot,
                                                    Time.deltaTime * bodyRotationSpeed);
        }
    }
}
