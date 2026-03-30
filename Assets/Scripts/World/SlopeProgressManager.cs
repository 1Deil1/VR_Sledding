using UnityEngine;

/// <summary>
/// Singleton that drives the progressive difficulty of the slope over time.
///
/// Attach this to any persistent GameObject (e.g. GameManager).
/// All other systems (TerrainGenerator, ObstacleSpawner, SledController) read
/// CurrentAngle and CurrentGravityScale from here.
/// </summary>
public class SlopeProgressManager : MonoBehaviour
{
    public static SlopeProgressManager Instance { get; private set; }

    [Header("Slope Angle")]
    [Tooltip("Starting slope angle in degrees.")]
    [SerializeField] private float startAngle = 10f;

    [Tooltip("Maximum slope angle the hill ramps up to.")]
    [SerializeField] private float maxAngle   = 25f;

    [Tooltip("How many seconds it takes to go from start angle to max angle.")]
    [SerializeField] private float rampDuration = 120f;

    [Tooltip("Animation curve controlling how the angle increases over time " +
             "(left = start, right = end). Linear by default.")]
    [SerializeField] private AnimationCurve rampCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Speed Scaling")]
    [Tooltip("At startAngle the gravity multiplier is 1.0. " +
             "At maxAngle it reaches this value, linearly scaling with angle.")]
    [SerializeField] private float maxGravityScale = 2.2f;

    // ── Public read-only state ────────────────────────────────────────────────

    /// <summary>Current slope angle in degrees (increases over time).</summary>
    public float CurrentAngle       { get; private set; }

    /// <summary>0-1 progress through the ramp (0 = start, 1 = max).</summary>
    public float Progress           { get; private set; }

    /// <summary>
    /// Multiplier for base gravity acceleration.
    /// Scales linearly from 1.0 at startAngle to maxGravityScale at maxAngle.
    /// </summary>
    public float CurrentGravityScale { get; private set; }

    private float _elapsed = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        CurrentAngle        = startAngle;
        CurrentGravityScale = 1f;
    }

    private void Update()
    {
        _elapsed  = Mathf.Min(_elapsed + Time.deltaTime, rampDuration);
        Progress  = rampDuration > 0f ? _elapsed / rampDuration : 1f;

        float t          = rampCurve.Evaluate(Progress);
        CurrentAngle     = Mathf.Lerp(startAngle, maxAngle, t);

        // Gravity scale: 1.0 at startAngle, maxGravityScale at maxAngle
        CurrentGravityScale = Mathf.Lerp(1f, maxGravityScale, t);
    }
}
