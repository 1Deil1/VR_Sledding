using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Attach to a World Space Canvas root to make it grabbable in VR.
///
/// SETUP (do this once per canvas):
///   1. Add this component to the Canvas GameObject.
///   2. Add a BoxCollider to the same GameObject — resize it to cover the whole panel.
///   3. Add a Rigidbody to the same GameObject — set Is Kinematic = true.
///   4. The XRGrabInteractable is added automatically via RequireComponent.
///   5. (Optional) Assign the XR Origin transform in the Inspector.
///      If left empty the script finds it automatically.
///
/// USAGE:
///   Point your VR controller at the panel and press the grip/select button to grab.
///   Move the panel to the position you want, then release.
///   The panel will lock to the camera rig and follow you from that position.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class DraggableUIPanel : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The XR Origin (camera rig) transform. Panel will follow this after being placed. " +
             "Leave empty to auto-detect.")]
    [SerializeField] private Transform xrOrigin;

    [Header("Behaviour")]
    [Tooltip("If true the panel keeps its upright world rotation while being dragged " +
             "(good for UI readability). If false it rotates freely with the hand.")]
    [SerializeField] private bool lockRotationWhileHeld = true;

    private XRGrabInteractable _grab;
    private Rigidbody          _rb;

    private void Awake()
    {
        // Configure Rigidbody so physics doesn't fling the canvas around
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.useGravity  = false;

        // Configure the grab interactable
        _grab = GetComponent<XRGrabInteractable>();
        _grab.trackPosition     = true;
        _grab.trackRotation     = !lockRotationWhileHeld;
        _grab.throwOnDetach     = false;

        _grab.selectEntered.AddListener(OnGrabbed);
        _grab.selectExited.AddListener(OnReleased);
    }

    private void Start()
    {
        // Auto-locate XR Origin if not assigned
        if (xrOrigin == null)
            xrOrigin = FindXROrigin();

        // Start parented to origin so it follows the player right away
        if (xrOrigin != null && transform.parent == null)
            transform.SetParent(xrOrigin, true);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        // Detach from player so the panel can move freely in space
        transform.SetParent(null, true);
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        // Re-find origin in case scene loaded after Awake
        if (xrOrigin == null)
            xrOrigin = FindXROrigin();

        if (xrOrigin != null)
        {
            // Parent to XR origin — world position/rotation preserved,
            // but from now on it moves with the player
            transform.SetParent(xrOrigin, true);
        }
    }

    private void OnDestroy()
    {
        if (_grab != null)
        {
            _grab.selectEntered.RemoveListener(OnGrabbed);
            _grab.selectExited.RemoveListener(OnReleased);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Transform FindXROrigin()
    {
        // Look for the VRCameraRig script which is on the XR Origin
        var rig = FindAnyObjectByType<VRCameraRig>();
        if (rig != null) return rig.transform;

        // Fallback: look for an object tagged MainCamera's root
        var cam = Camera.main;
        if (cam != null) return cam.transform.root;

        return null;
    }
}
