using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState { WaitingForPhone, Playing, Crashed, GameOver }

/// <summary>
/// Central game state machine. Controls game flow from waiting for phone
/// connection through playing, crash, and game-over states.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState State { get; private set; } = GameState.WaitingForPhone;

    [Header("References")]
    [SerializeField] private SledController sledController;
    [SerializeField] private ScoreManager   scoreManager;
    [SerializeField] private GameUI         gameUI;

    [Header("Settings")]
    [SerializeField] private float crashSlowdownTime = 2f;   // Seconds before showing game-over panel

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (State == GameState.WaitingForPhone)
        {
            // Automatically start once the phone connects
            if (RelayClient.Instance != null && RelayClient.Instance.IsPhoneConnected)
                StartGame();
        }
    }

    // ── State transitions ─────────────────────────────────────────────────────

    private void StartGame()
    {
        State = GameState.Playing;
        scoreManager?.StartTracking();
        gameUI?.ShowPlayingUI();
        Debug.Log("[GameManager] Game started — phone connected.");
    }

    /// <summary>Called by Obstacle when the sled touches a tree or rock.</summary>
    public void OnSledHitObstacle()
    {
        if (State != GameState.Playing) return;

        State = GameState.Crashed;
        scoreManager?.StopTracking();
        gameUI?.ShowCrashUI();
        Invoke(nameof(TransitionToGameOver), crashSlowdownTime);
        Debug.Log("[GameManager] Sled hit obstacle!");
    }

    private void TransitionToGameOver()
    {
        State = GameState.GameOver;
        gameUI?.ShowGameOverUI();
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
