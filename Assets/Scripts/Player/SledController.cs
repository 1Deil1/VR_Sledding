using UnityEngine;

/// <summary>
/// Controls the sled movement based on phone tilt input received from PhoneInputServer.
/// Pitch controls speed (forward lean = faster, backward lean = brake).
/// Roll controls steering (left/right lean).
/// The sled always moves forward along the slope; gravity is simulated manually.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SledController : MonoBehaviour
{
    [Header("Speed Settings")]
    [SerializeField] private float baseGravityAcceleration = 4f;    // Natural downhill pull
    [SerializeField] private float maxSpeed                = 30f;
    [SerializeField] private float minSpeed                = 1f;
    [SerializeField] private float pitchAccelMultiplier    = 1.5f;  // Extra accel when leaning forward
    [SerializeField] private float brakeMultiplier         = 3f;    // Decel when leaning back

    [Header("Steering Settings")]
    [Tooltip("Maximum yaw rotation applied per second at full roll tilt.")]
    [SerializeField] private float maxSteerDegreesPerSecond = 35f;
    [Tooltip("Fraction of roll input mapped to steering. Lower = more subtle.")]
    [SerializeField, Range(0.1f, 1f)] private float steerSensitivity = 0.4f;
    [Tooltip("How quickly the steering rate returns to zero when phone is level.")]
    [SerializeField, Range(1f, 20f)] private float steerDamping = 6f;

    [Header("Tilt Dead Zones")]
    [SerializeField] private float pitchDeadZone = 8f;              // Degrees ignored before registering
    [SerializeField] private float rollDeadZone  = 6f;

    [Header("Ground Detection")]
    [SerializeField] private float     groundRayLength = 2.0f;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody _rb;
    private float     _currentSpeed   = 0f;
    private float     _currentSteer   = 0f;   // Smoothed steering rate (°/s)
    private bool      _isGrounded     = false;
    private Vector3   _smoothedNormal = Vector3.up;  // Blended surface normal

    [Header("Surface Smoothing")]
    [Tooltip("How quickly the sled adapts to a new surface normal. "
           + "High = snappy, low = floaty over bumps.")]
    [SerializeField, Range(1f, 30f)] private float surfaceNormalSmoothing = 12f;

    private void Awake()
    {
        _rb            = GetComponent<Rigidbody>();
        _rb.useGravity = false;  // We handle gravity manually along the slope
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private void FixedUpdate()
    {
        if (PhoneInputServer.Instance == null) return;

        float pitch = PhoneInputServer.Instance.Pitch;
        float roll  = PhoneInputServer.Instance.Roll;

        CheckGrounded();
        HandleSpeed(pitch);
        HandleSteering(roll);
        ApplyMovement();
    }

    // ── Speed ─────────────────────────────────────────────────────────────────

    private void HandleSpeed(float pitch)
    {
        float effectivePitch = Mathf.Abs(pitch) > pitchDeadZone ? pitch : 0f;

        // Scale gravity with slope angle — steeper slope = faster natural speed.
        float gravityScale = SlopeProgressManager.Instance != null
                           ? SlopeProgressManager.Instance.CurrentGravityScale
                           : 1f;
        _currentSpeed += baseGravityAcceleration * gravityScale * Time.fixedDeltaTime;

        if (effectivePitch > 0f)        // Lean forward  → accelerate
            _currentSpeed += effectivePitch * pitchAccelMultiplier * Time.fixedDeltaTime;
        else if (effectivePitch < 0f)   // Lean backward → brake
            _currentSpeed += effectivePitch * brakeMultiplier * Time.fixedDeltaTime;

        _currentSpeed = Mathf.Clamp(_currentSpeed, minSpeed, maxSpeed);
    }

    // ── Steering ──────────────────────────────────────────────────────────────

    private void HandleSteering(float roll)
    {
        // Apply dead zone, then normalise into -1..1 within the clamped input range
        // (maxRollInput = 40° by default, so a 20° lean → 0.5 → gentle turn).
        float effectiveRoll = Mathf.Abs(roll) > rollDeadZone ? roll : 0f;
        float targetSteer   = (effectiveRoll / 40f) * maxSteerDegreesPerSecond * steerSensitivity;

        // Smoothly blend the steering rate so sudden tilts don't snap the sled.
        _currentSteer = Mathf.Lerp(_currentSteer, targetSteer, Time.fixedDeltaTime * steerDamping);

        transform.Rotate(0f, _currentSteer * Time.fixedDeltaTime, 0f, Space.World);
    }

    // ── Movement ──────────────────────────────────────────────────────────────

    private void ApplyMovement()
    {
        Vector3 moveDir = transform.forward;

        if (_isGrounded)
        {
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit,
                                groundRayLength, groundLayer))
            {
                // Blend the surface normal so seam transitions between terrain segments
                // never cause an abrupt velocity direction change.
                _smoothedNormal = Vector3.Lerp(_smoothedNormal, hit.normal,
                                               Time.fixedDeltaTime * surfaceNormalSmoothing);

                moveDir = Vector3.ProjectOnPlane(transform.forward, _smoothedNormal).normalized;

                Quaternion targetRot =
                    Quaternion.FromToRotation(transform.up, _smoothedNormal) * transform.rotation;
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                                                      Time.fixedDeltaTime * 10f);
            }
        }
        else
        {
            // Airborne: gradually return normal to world-up so landing is smooth
            _smoothedNormal = Vector3.Lerp(_smoothedNormal, Vector3.up,
                                           Time.fixedDeltaTime * 3f);
            _rb.AddForce(Physics.gravity, ForceMode.Acceleration);
        }

        _rb.linearVelocity = moveDir * _currentSpeed;
    }

    private void CheckGrounded()
    {
        _isGrounded = Physics.Raycast(transform.position, Vector3.down,
                                      groundRayLength, groundLayer);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Returns current speed in units/s (shown in UI as km/h label).</summary>
    public float GetCurrentSpeed() => _currentSpeed;
}
