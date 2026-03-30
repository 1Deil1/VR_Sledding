using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns trees and rocks randomly across the slope as the sled progresses.
/// Old obstacles that are behind the sled are destroyed to manage memory.
/// </summary>
[DefaultExecutionOrder(100)]
public class ObstacleSpawner : MonoBehaviour
{
    [Header("Obstacle Prefabs")]
    [SerializeField] private GameObject[] treePrefabs;
    [SerializeField] private GameObject[] rockPrefabs;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnDistanceAhead    = 550f;
    [SerializeField] private float despawnDistanceBehind = 100f;

    [Header("Density")]
    [Tooltip("0 = very sparse (one obstacle every ~8 m).\n"
           + "0.5 = medium (every ~4 m).\n"
           + "1 = very dense (every ~1 m).\n"
           + "Change this at any time — new obstacles will use the updated density immediately.")]
    [SerializeField, Range(0f, 1f)] private float density = 0.5f;

    [Tooltip("Hard cap on how many trees/rocks can exist in the scene at once. "
           + "0 = unlimited. Raise this to see more, lower it for better performance.")]
    [SerializeField] private int maxVisibleObstacles = 300;

    [Tooltip("Width of the spawn corridor. Wider = trees/rocks cover more of the slope sides.")]
    [SerializeField] private float laneWidth = 18f;

    [Tooltip("Fraction of spawned obstacles that are trees (rest are rocks).")]
    [SerializeField, Range(0f, 1f)] private float treeProbability = 0.65f;

    // Density maps to a spawn interval: density=0 → 8 m apart, density=1 → 1 m apart.
    // Using 1/lerp so the slider feels linear in terms of visible crowding.
    private float SpawnInterval => Mathf.Lerp(8f, 1f, density);
    private float SpawnJitter   => SpawnInterval * 0.3f;

    [Header("Ground Detection")]
    [Tooltip("Set this to the same layer used by your TerrainSegment prefab (e.g. \"Ground\").")]
    [SerializeField] private LayerMask groundLayer = Physics.DefaultRaycastLayers;

    [Header("References")]
    [SerializeField] private Transform       sledTransform;
    [Tooltip("Assign the TerrainGenerator so obstacles never spawn beyond live terrain.")]
    [SerializeField] private TerrainGenerator terrainGenerator;

    [Header("Obstacle Placement")]
    [Tooltip("How far to sink obstacles into the ground. Hides the floating edge on slopes.")]
    [SerializeField] private float groundSinkAmount = 0.3f;

    [Tooltip("Re-snap every active obstacle to the terrain this often (seconds). "
           + "Keeps obstacles on the surface as the slope angle changes.")]
    [SerializeField] private float resnapInterval = 0.4f;

    private float                _nextSpawnZ   = 20f;
    private float                _resnapTimer  = 0f;
    private bool                 _initialized  = false;
    private readonly List<GameObject> _active  = new List<GameObject>();

    private void Update()
    {
        if (sledTransform == null) return;

        float sledZ = sledTransform.position.z;
        float sledY = sledTransform.position.y;

        // ── First-frame init ──────────────────────────────────────────────────
        // Done here (not in Start) so that TerrainGenerator.Start() is guaranteed
        // to have already run and all terrain segments physically exist.
        // Unity always completes ALL Start() calls before the first Update().
        if (!_initialized)
        {
            _initialized = true;
            float maxSpawnZ0 = sledZ + spawnDistanceAhead;
            if (terrainGenerator != null)
                maxSpawnZ0 = Mathf.Min(maxSpawnZ0, terrainGenerator.SpawnHeadZ - 5f);
            while (_nextSpawnZ < maxSpawnZ0)
            {
                if (maxVisibleObstacles > 0 && _active.Count >= maxVisibleObstacles) break;
                SpawnAt(_nextSpawnZ);
                _nextSpawnZ += SpawnInterval + Random.Range(-SpawnJitter, SpawnJitter);
            }
        }

        // Spawn new obstacles ahead of the sled, but never past the terrain boundary.
        float maxSpawnZ = sledZ + spawnDistanceAhead;
        if (terrainGenerator != null)
            maxSpawnZ = Mathf.Min(maxSpawnZ, terrainGenerator.SpawnHeadZ - 5f);

        while (_nextSpawnZ < maxSpawnZ)
        {
            if (maxVisibleObstacles > 0 && _active.Count >= maxVisibleObstacles) break;
            SpawnAt(_nextSpawnZ);
            _nextSpawnZ += SpawnInterval + Random.Range(-SpawnJitter, SpawnJitter);
        }

        // Remove obstacles that are far behind.
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (_active[i] == null || _active[i].transform.position.z < sledZ - despawnDistanceBehind)
            {
                if (_active[i] != null) Destroy(_active[i]);
                _active.RemoveAt(i);
            }
        }

        // ── Periodic re-snap ──────────────────────────────────────────────────
        // Corrects any obstacle whose placement drifted as the slope changes.
        _resnapTimer += Time.deltaTime;
        if (_resnapTimer >= resnapInterval)
        {
            _resnapTimer = 0f;
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i] == null) continue;
                SnapToGround(_active[i], sledY);
            }
        }
    }

    private void SpawnAt(float z)
    {
        bool       spawnTree = Random.value < treeProbability;
        GameObject[] pool    = spawnTree ? treePrefabs : rockPrefabs;
        if (pool == null || pool.Length == 0) return;

        float x    = Random.Range(-laneWidth * 0.5f, laneWidth * 0.5f);
        float sledY = sledTransform != null ? sledTransform.position.y : 0f;

        // Raycast BEFORE instantiating the prefab so the prefab's own colliders
        // cannot interfere with the hit result.
        Vector3 rayOrigin  = new Vector3(x, sledY + 100f, z);
        Vector3 surfacePos = new Vector3(x, sledY - 300f, z); // Fallback
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 500f, groundLayer))
            surfacePos = hit.point;

        GameObject prefab = pool[Random.Range(0, pool.Length)];
        GameObject obs    = Instantiate(prefab, surfacePos, Quaternion.identity);

        // Keep upright in world space (trees grow against gravity, not along slope).
        obs.transform.rotation = Quaternion.Euler(0f, obs.transform.eulerAngles.y, 0f);

        // Raise pivot so mesh base sits flush; sink slightly to hide floating slope edge.
        SnapBoundsToSurface(obs);

        // Lock any Rigidbody kinematic so physics cannot pull the obstacle underground
        // when terrain segments behind it are later destroyed during cleanup.
        foreach (Rigidbody rb in obs.GetComponentsInChildren<Rigidbody>())
            rb.isKinematic = true;

        _active.Add(obs);
    }

    // ── Snapping helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Raycasts from above the sled down to the terrain and moves obs to the hit point,
    /// then calls SnapBoundsToSurface to align the mesh base.
    /// Uses RaycastAll so we can skip hits on the obstacle's own colliders —
    /// otherwise the ray would hit the tree/rock itself and push it upward each tick.
    /// </summary>
    private void SnapToGround(GameObject obs, float sledY)
    {
        Vector3 origin = new Vector3(obs.transform.position.x, sledY + 100f,
                                     obs.transform.position.z);

        // Temporarily disable the obstacle's own colliders so the downward ray
        // cannot hit them (IsChildOf checks are fragile on some prefab hierarchies
        // and can cause the tree to creep upward each re-snap tick).
        Collider[] ownColliders = obs.GetComponentsInChildren<Collider>();
        for (int i = 0; i < ownColliders.Length; i++) ownColliders[i].enabled = false;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 500f, groundLayer))
        {
            obs.transform.position = hit.point;
            SnapBoundsToSurface(obs);
        }

        for (int i = 0; i < ownColliders.Length; i++) ownColliders[i].enabled = true;
    }

    /// <summary>Adjusts the obstacle Y so its mesh bottom sits at transform.position Y,
    /// then sinks it by groundSinkAmount to hide the floating slope edge.
    /// Uses sharedMesh local bounds (never stale) instead of Renderer world bounds.</summary>
    private void SnapBoundsToSurface(GameObject obs)
    {
        // Prefer local mesh bounds — accurate on the same frame as Instantiate.
        MeshFilter mf = obs.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            // localBottom: Y distance from pivot to bottom of mesh in local space,
            // scaled to world units. Negative for centre-pivoted meshes.
            float localBottom = mf.sharedMesh.bounds.min.y * obs.transform.lossyScale.y;
            // Raise pivot so mesh base lands exactly on surface, then apply sink.
            obs.transform.position += Vector3.up * (-localBottom - groundSinkAmount);
            return;
        }
        // Fallback for prefabs without a MeshFilter at root (e.g. LOD groups).
        Renderer rend = obs.GetComponentInChildren<Renderer>();
        if (rend == null) return;
        float bottomOffset = obs.transform.position.y - rend.bounds.min.y;
        obs.transform.position += Vector3.up * (bottomOffset - groundSinkAmount);
    }
}
