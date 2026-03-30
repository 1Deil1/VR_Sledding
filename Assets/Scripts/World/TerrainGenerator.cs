using UnityEngine;

/// <summary>
/// Generates an infinite-feeling slope by spawning tiled terrain segments ahead
/// of the sled and destroying old ones behind it.
///
/// Workflow:
///   1. Assign a TerrainSegment prefab (Plane + MeshCollider, Layer = "Ground").
///   2. Drag the Sled transform into sledTransform.
///   3. Tune segmentLength and slopeAngle to match your visual.
/// </summary>
[DefaultExecutionOrder(-100)]
public class TerrainGenerator : MonoBehaviour
{
    [Header("Segment Settings")]
    [SerializeField] private GameObject terrainSegmentPrefab;
    [SerializeField] private int        segmentsAhead  = 16;   // 16 × 50 m = 800 m ahead
    [SerializeField] private float      segmentLength  = 50f;

    [Header("Slope Transition")]
    [Tooltip("Maximum degrees the slope angle may change per new segment. "
           + "Keep this very small (0.2-0.5) so adjacent segments are nearly parallel "
           + "and the physical seam between them is imperceptible.")]
    [SerializeField] private float maxAngleChangePerSegment = 0.3f;

    [Header("Cleanup")]
    [SerializeField] private int segmentsBehind = 4;            // How many to keep behind the sled

    [Header("References")]
    [SerializeField] private Transform sledTransform;

    private int   _lastSpawnedIndex  = -1;
    // _spawnHeadY / _spawnHeadZ track the world-space SEAM position — the exact
    // point where the BACK EDGE of the next segment should be placed.
    // Each segment's CENTER is offset forward by half a segment length from this seam.
    // This guarantees the front edge of segment N always perfectly meets the back edge
    // of segment N+1 regardless of whether the angle changed between spawns.
    private float _spawnHeadY        = 0f;
    private float _spawnHeadZ        = 0f;
    private float _currentSpawnAngle = -1f;  // Initialised from SlopeProgressManager on Start

    /// <summary>World-space Z of the far edge of the last spawned segment.
    /// ObstacleSpawner uses this as a hard cap so obstacles never spawn beyond live terrain.</summary>
    public float SpawnHeadZ => _spawnHeadZ;

    /// <summary>World-space Y of the far edge of the last spawned segment.
    /// ObstacleSpawner uses this to anchor raycasts to the actual terrain depth.</summary>
    public float SpawnHeadY => _spawnHeadY;

    private void Start()
    {
        // Initialise spawn angle from SlopeProgressManager startAngle so the first
        // segments are all at the same angle and perfectly flush.
        _currentSpawnAngle = SlopeProgressManager.Instance != null
                           ? SlopeProgressManager.Instance.CurrentAngle
                           : 15f;

        // Pre-spawn segments so the sled starts on solid ground
        int initialCount = segmentsAhead + 2;
        for (int i = 0; i < initialCount; i++)
            SpawnSegment(i);

        _lastSpawnedIndex = initialCount - 1;
    }

    private void Update()
    {
        if (sledTransform == null || terrainSegmentPrefab == null) return;

        // Progress measured in whole segment lengths
        int sledSegmentIndex = Mathf.FloorToInt(sledTransform.position.z / segmentLength);

        // Spawn new segments ahead
        while (_lastSpawnedIndex < sledSegmentIndex + segmentsAhead)
        {
            _lastSpawnedIndex++;
            SpawnSegment(_lastSpawnedIndex);
        }

        // Destroy segments that are far enough behind (by name)
        int destroyBefore = sledSegmentIndex - segmentsBehind;
        GameObject old = GameObject.Find($"TerrainSegment_{destroyBefore}");
        if (old != null) Destroy(old);
    }

    private void SpawnSegment(int index)
    {
        if (terrainSegmentPrefab == null) return;

        // Advance spawn angle slowly towards the target. maxAngleChangePerSegment
        // is kept small so adjacent segments are nearly parallel and the seam is subtle.
        float targetAngle  = SlopeProgressManager.Instance != null
                           ? SlopeProgressManager.Instance.CurrentAngle
                           : 15f;
        _currentSpawnAngle = Mathf.MoveTowards(_currentSpawnAngle, targetAngle,
                                               maxAngleChangePerSegment);
        float angle        = _currentSpawnAngle;
        float sinA         = Mathf.Sin(angle * Mathf.Deg2Rad);
        float cosA         = Mathf.Cos(angle * Mathf.Deg2Rad);
        float halfLen      = segmentLength * 0.5f;

        // Place the CENTER of this segment half-a-segment forward from the seam,
        // so its back edge lands exactly on the seam (the front edge of the previous segment).
        Vector3 pos = new Vector3(
            0f,
            _spawnHeadY - halfLen * sinA,
            _spawnHeadZ + halfLen * cosA);

        GameObject seg = Instantiate(terrainSegmentPrefab, pos,
                                     Quaternion.Euler(angle, 0f, 0f));
        seg.name = $"TerrainSegment_{index}";

        // Advance the seam to the FRONT edge of this segment (= back edge of next segment).
        _spawnHeadY -= segmentLength * sinA;
        _spawnHeadZ += segmentLength * cosA;
    }
}
