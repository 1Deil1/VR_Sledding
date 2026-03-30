using UnityEngine;

/// <summary>
/// Creates a beautiful, animated fog bank — a front wall AND two side curtains —
/// that wraps around the sled to hide obstacle and terrain pop-in on all sides.
///
///   • Front fog:  planes perpendicular to the direction of travel, stacked at depth
///   • Side fog:   planes running parallel to the slope, one on each flank
///   • Both sets thicken gradually as the slope steepens
///   • Scene fog (RenderSettings) blends everything together seamlessly
///
/// ─── ZERO SETUP REQUIRED ───────────────────────────────────────────────────
///   1. Add this component to any active GameObject (e.g. your Managers object).
///   2. Assign "Sled Transform" in the Inspector.
///   3. Hit Play — fog appears automatically everywhere.
///
/// The shader (Assets/Shaders/FogPlane.shader) must exist in the project.
/// All fog plane GameObjects are created entirely at runtime.
/// </summary>
public class SledFogController : MonoBehaviour
{
    // ── References ─────────────────────────────────────────────────────────────
    [Header("References")]
    [Tooltip("The sled's root Transform — the same one SledController lives on.")]
    [SerializeField] private Transform sledTransform;

    // ── Front Fog Wall ─────────────────────────────────────────────────────────
    [Header("Front Fog Wall")]
    [Tooltip("Distance ahead (along the slope) where the NEAREST front fog layer appears.\n"
           + "Set this a bit behind spawnDistanceAhead in ObstacleSpawner.")]
    [SerializeField] private float nearLayerDistance = 310f;

    [Tooltip("Gap between successive front fog layers in metres.")]
    [SerializeField] private float layerSpacing = 38f;

    [Tooltip("Number of stacked front fog planes. 3 gives convincing depth.")]
    [SerializeField, Range(2, 6)] private int layerCount = 3;

    [Tooltip("Width of each front fog plane. Keep wider than your terrain.")]
    [SerializeField] private float planeWidth  = 480f;

    [Tooltip("Height of each front fog plane. Taller covers sky and ground equally.")]
    [SerializeField] private float planeHeight = 110f;

    [Tooltip("Base opacity of the front fog layers. 0 = invisible, 1 = solid wall.\n"
           + "Nearest layer is ~55 % of this; farthest is 100 %.")]
    [SerializeField, Range(0f, 1f)] private float baseDensity = 0.48f;

    // ── Side Fog Curtains ──────────────────────────────────────────────────────
    [Header("Side Fog Curtains")]
    [Tooltip("How far to the left/right of the sled the innermost side layer sits.\n"
           + "Match this roughly to half your laneWidth in ObstacleSpawner (~9 m).")]
    [SerializeField] private float sideOffset = 9f;

    [Tooltip("Gap between successive side layers (they stack outward). Wider = softer edge.")]
    [SerializeField] private float sideLayerSpacing = 14f;

    [Tooltip("Number of side fog planes per side (left and right each get this many).")]
    [SerializeField, Range(1, 4)] private int sideLayerCount = 2;

    [Tooltip("How deep (along the slope) each side curtain extends.\n"
           + "Should reach from slightly behind the sled to past the front fog wall.")]
    [SerializeField] private float sideDepth = 400f;

    [Tooltip("The side curtains are centred this far ahead of the sled (along slope).")]
    [SerializeField] private float sideZCenter = 150f;

    [Tooltip("Height of the side fog curtains in metres.\nIncrease to cover more of the sky; decrease to hug the ground.")]
    [SerializeField] private float sideHeight = 110f;

    [Tooltip("Opacity of the side fog layers relative to the front fog base density.")]
    [SerializeField, Range(0f, 1f)] private float sideDensityMultiplier = 0.55f;

    // ── Colour ─────────────────────────────────────────────────────────────────
    [Header("Colour")]
    [Tooltip("Colour for the fog planes and the atmospheric scene fog.\n"
           + "A subtle blue-grey mimics cold mountain haze.")]
    [SerializeField] private Color fogColor = new Color(0.80f, 0.87f, 0.95f, 1f);

    // ── Atmospheric Scene Fog ──────────────────────────────────────────────────
    [Header("Atmospheric Scene Fog")]
    [Tooltip("Take control of Unity's built-in scene fog (RenderSettings).\n"
           + "Disable if you manage fog via a post-process volume instead.")]
    [SerializeField] private bool controlSceneFog = true;

    [Tooltip("Scene fog starts blending in at this distance from the camera.")]
    [SerializeField] private float fogStartDistance = 170f;

    [Tooltip("Scene fog is fully opaque at this distance.")]
    [SerializeField] private float fogEndDistance   = 490f;

    // ── Internal ────────────────────────────────────────────────────────────────
    private Material              _fogMat;
    private GameObject[]          _frontLayers;
    private GameObject[]          _leftLayers;
    private GameObject[]          _rightLayers;
    private MaterialPropertyBlock _mpb;

    // All layers in one flat list — used only for cleanup in OnDestroy.
    private GameObject[] _allLayers;

    private const float kProgressDensityBonus = 0.14f;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        CreateFogMaterial();
        BuildFrontLayers();
        BuildSideLayers();

        // Flat list for easy cleanup.
        int total = _frontLayers.Length + _leftLayers.Length + _rightLayers.Length;
        _allLayers = new GameObject[total];
        int idx = 0;
        foreach (var g in _frontLayers) _allLayers[idx++] = g;
        foreach (var g in _leftLayers)  _allLayers[idx++] = g;
        foreach (var g in _rightLayers) _allLayers[idx++] = g;
    }

    private void Start()
    {
        if (controlSceneFog)
        {
            RenderSettings.fog              = true;
            RenderSettings.fogMode          = FogMode.Linear;
            RenderSettings.fogColor         = fogColor;
            RenderSettings.fogStartDistance = fogStartDistance;
            RenderSettings.fogEndDistance   = fogEndDistance;
        }
    }

    private void LateUpdate()
    {
        if (sledTransform == null) return;

        PositionFrontLayers();
        PositionSideLayers();
        AnimateDensity();
    }

    private void OnDestroy()
    {
        if (_allLayers != null)
            foreach (GameObject g in _allLayers)
                if (g != null) Destroy(g);

        if (_fogMat != null) Destroy(_fogMat);
    }

    // ── Material creation ──────────────────────────────────────────────────────

    private void CreateFogMaterial()
    {
        Shader sh = Shader.Find("Custom/FogPlane");
        if (sh == null)
        {
            Debug.LogError("[SledFogController] Shader 'Custom/FogPlane' not found. " +
                           "Make sure Assets/Shaders/FogPlane.shader exists.");
            return;
        }
        _fogMat = new Material(sh) { name = "FogPlane_RuntimeMat" };
        _fogMat.SetColor("_FogColor", fogColor);
    }

    // ── Layer construction ─────────────────────────────────────────────────────

    private void BuildFrontLayers()
    {
        if (_fogMat == null) { _frontLayers = new GameObject[0]; return; }

        Mesh quad     = MakeQuadMesh(planeWidth, planeHeight);
        _frontLayers  = new GameObject[layerCount];

        for (int i = 0; i < layerCount; i++)
        {
            float t       = layerCount > 1 ? (float)i / (layerCount - 1) : 0f;
            float density = Mathf.Lerp(baseDensity * 0.55f, baseDensity, t);
            _frontLayers[i] = MakeFogPlane($"FogFront_{i}", quad, density, i * 1.41f);
        }
    }

    private void BuildSideLayers()
    {
        if (_fogMat == null)
        {
            _leftLayers  = new GameObject[0];
            _rightLayers = new GameObject[0];
            return;
        }

        // Side planes are rotated 90° around Y so their surface faces left/right.
        // Their quad width maps to the Z axis (slope direction) after rotation.
        Mesh sideQuad  = MakeQuadMesh(sideDepth, sideHeight);
        _leftLayers    = new GameObject[sideLayerCount];
        _rightLayers   = new GameObject[sideLayerCount];

        for (int i = 0; i < sideLayerCount; i++)
        {
            // Innermost layer is lightest; outer layer is denser.
            float t       = sideLayerCount > 1 ? (float)i / (sideLayerCount - 1) : 0f;
            float density = Mathf.Lerp(baseDensity * sideDensityMultiplier * 0.5f,
                                       baseDensity * sideDensityMultiplier, t);
            // Phase offset continues from where front layers left off.
            float phase = (layerCount + i) * 1.41f;
            _leftLayers[i]  = MakeFogPlane($"FogLeft_{i}",  sideQuad, density, phase);
            _rightLayers[i] = MakeFogPlane($"FogRight_{i}", sideQuad, density, phase + 0.7f);
        }
    }

    /// <summary>Creates a single fog plane GameObject, sets initial density & phase.</summary>
    private GameObject MakeFogPlane(string name, Mesh quad, float density, float phase)
    {
        GameObject go         = new GameObject(name);
        go.transform.SetParent(null);

        MeshFilter   mf       = go.AddComponent<MeshFilter>();
        MeshRenderer mr       = go.AddComponent<MeshRenderer>();
        mf.sharedMesh         = quad;
        mr.sharedMaterial     = _fogMat;
        mr.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows     = false;

        _mpb.Clear();
        _mpb.SetFloat("_Density",     density);
        _mpb.SetFloat("_PhaseOffset", phase);
        mr.SetPropertyBlock(_mpb);

        return go;
    }

    // ── Per-frame position updates ─────────────────────────────────────────────

    private void PositionFrontLayers()
    {
        Vector3 sledPos  = sledTransform.position;
        float   angleRad = GetSlopeAngleRad();
        float   cosA     = Mathf.Cos(angleRad);
        float   sinA     = Mathf.Sin(angleRad);

        for (int i = 0; i < _frontLayers.Length; i++)
        {
            if (_frontLayers[i] == null) continue;

            float   dist = nearLayerDistance + i * layerSpacing;
            Vector3 pos  = new Vector3(
                sledPos.x,
                sledPos.y - sinA * dist,
                sledPos.z + cosA * dist
            );
            _frontLayers[i].transform.position = pos;
            _frontLayers[i].transform.rotation = Quaternion.identity;
        }
    }

    private void PositionSideLayers()
    {
        Vector3 sledPos  = sledTransform.position;
        float   angleRad = GetSlopeAngleRad();
        float   cosA     = Mathf.Cos(angleRad);
        float   sinA     = Mathf.Sin(angleRad);

        // Centre of the side curtain along the slope.
        Vector3 slopeCenterOffset = new Vector3(0f, -sinA * sideZCenter, cosA * sideZCenter);

        // Side planes face along X (rotated 90° around Y).
        Quaternion sideRot = Quaternion.Euler(0f, 90f, 0f);

        for (int i = 0; i < _leftLayers.Length; i++)
        {
            if (_leftLayers[i] == null || _rightLayers[i] == null) continue;

            // Each successive layer steps one spacing unit further out from the sled.
            float xOffset = sideOffset + i * sideLayerSpacing;

            Vector3 center = sledPos + slopeCenterOffset;

            _leftLayers[i].transform.position  = center + new Vector3(-xOffset, 0f, 0f);
            _leftLayers[i].transform.rotation  = sideRot;

            _rightLayers[i].transform.position = center + new Vector3( xOffset, 0f, 0f);
            _rightLayers[i].transform.rotation = sideRot;
        }
    }

    // ── Density animation ──────────────────────────────────────────────────────

    private void AnimateDensity()
    {
        float progress = SlopeProgressManager.Instance != null
                       ? SlopeProgressManager.Instance.Progress : 0f;
        float bonus    = progress * kProgressDensityBonus;

        // Front layers
        for (int i = 0; i < _frontLayers.Length; i++)
        {
            if (_frontLayers[i] == null) continue;
            float t       = layerCount > 1 ? (float)i / (layerCount - 1) : 0f;
            float density = Mathf.Lerp(baseDensity * 0.55f, baseDensity, t) + bonus;
            SetDensity(_frontLayers[i], density);
        }

        // Side layers (left & right together)
        for (int i = 0; i < _leftLayers.Length; i++)
        {
            float t       = sideLayerCount > 1 ? (float)i / (sideLayerCount - 1) : 0f;
            float density = Mathf.Lerp(baseDensity * sideDensityMultiplier * 0.5f,
                                       baseDensity * sideDensityMultiplier, t)
                          + bonus * 0.6f; // sides thicken more gently

            if (_leftLayers[i]  != null) SetDensity(_leftLayers[i],  density);
            if (_rightLayers[i] != null) SetDensity(_rightLayers[i], density);
        }
    }

    private void SetDensity(GameObject go, float density)
    {
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.GetPropertyBlock(_mpb);
        _mpb.SetFloat("_Density", density);
        mr.SetPropertyBlock(_mpb);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static float GetSlopeAngleRad()
    {
        if (SlopeProgressManager.Instance != null)
            return SlopeProgressManager.Instance.CurrentAngle * Mathf.Deg2Rad;
        return 0f;
    }

    private static Mesh MakeQuadMesh(float width, float height)
    {
        float hw = width  * 0.5f;
        float hh = height * 0.5f;

        Mesh mesh = new Mesh { name = "FogQuad" };
        mesh.vertices  = new Vector3[]
        {
            new Vector3(-hw, -hh, 0f),
            new Vector3( hw, -hh, 0f),
            new Vector3(-hw,  hh, 0f),
            new Vector3( hw,  hh, 0f),
        };
        mesh.uv        = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
        };
        mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateNormals();
        return mesh;
    }
}
