using UnityEngine;

/// <summary>
/// Tracks the distance the sled has travelled and derives a score from it.
/// Score = distance in whole metres. High score is persisted via PlayerPrefs.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    public float Distance  { get; private set; } = 0f;
    public int   Score     => Mathf.FloorToInt(Distance);
    public int   HighScore => PlayerPrefs.GetInt("HighScore", 0);

    [SerializeField] private Transform sledTransform;

    private Vector3 _lastPosition;
    private bool    _isTracking = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (sledTransform != null)
            _lastPosition = sledTransform.position;
    }

    private void Update()
    {
        if (!_isTracking || sledTransform == null) return;

        Distance     += Vector3.Distance(sledTransform.position, _lastPosition);
        _lastPosition = sledTransform.position;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void StartTracking()
    {
        Distance     = 0f;
        _isTracking  = true;
        if (sledTransform != null)
            _lastPosition = sledTransform.position;
    }

    public void StopTracking()
    {
        _isTracking = false;

        if (Score > HighScore)
            PlayerPrefs.SetInt("HighScore", Score);
    }
}
