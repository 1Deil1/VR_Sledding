using UnityEngine;
using TMPro;

/// <summary>
/// Manages all in-game UI panels using Unity's canvas system.
///
/// IMPORTANT: Use a World Space Canvas in VR — Screen Space Overlay is not
/// supported on XR headsets. Position the canvas at roughly (0, 1.5, 3)
/// relative to the sled with scale (0.002, 0.002, 0.002).
/// </summary>
public class GameUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject waitingForPhonePanel;
    [SerializeField] private GameObject playingPanel;
    [SerializeField] private GameObject crashPanel;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Playing HUD")]
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI highScoreText;

    [Header("Connection Info (Waiting panel)")]
    [SerializeField] private TextMeshProUGUI ipAddressText;

    [Header("Game Over")]
    [SerializeField] private TextMeshProUGUI finalScoreText;
    [SerializeField] private TextMeshProUGUI gameOverHighScoreText;

    [Header("References")]
    [SerializeField] private SledController sledController;

    private void Start()
    {
        ShowWaitingForPhoneUI();

        // Show both URLs so the player knows which one to open on their phone
        if (PhoneInputServer.Instance != null && ipAddressText != null)
        {
            string ip = PhoneInputServer.Instance.LocalIPAddress;
            ipAddressText.text =
                $"<b>Android</b>\nOpen in browser:\nhttp://{ip}:8081/controller\n\n" +
                $"<b>iPhone</b>\n" +
                $"1. Safari \u2192 http://{ip}:8081/download\n" +
                $"   (file downloads automatically)\n" +
                $"2. Files app \u2192 On My iPhone \u2192 Downloads\n" +
                $"   Tap sled_controller.html\n" +
                $"3. Enter IP \u2192 Connect & Start \u2192 Allow";
        }
    }

    private void Update()
    {
        // Keep HUD values fresh while playing
        if (playingPanel != null && playingPanel.activeSelf)
        {
            if (sledController != null && speedText != null)
                speedText.text = $"{sledController.GetCurrentSpeed():F1} km/h";

            if (ScoreManager.Instance != null && scoreText != null)
                scoreText.text = $"{ScoreManager.Instance.Score}m";
        }
    }

    // ── Panel switching ───────────────────────────────────────────────────────

    public void ShowWaitingForPhoneUI()
    {
        SetAllPanels(false);
        waitingForPhonePanel?.SetActive(true);
    }

    public void ShowPlayingUI()
    {
        SetAllPanels(false);
        playingPanel?.SetActive(true);

        if (highScoreText != null && ScoreManager.Instance != null)
            highScoreText.text = $"Best: {ScoreManager.Instance.HighScore}m";
    }

    public void ShowCrashUI()
    {
        // Overlay the crash panel on top of the playing panel
        crashPanel?.SetActive(true);
    }

    public void ShowGameOverUI()
    {
        SetAllPanels(false);
        gameOverPanel?.SetActive(true);

        if (ScoreManager.Instance != null)
        {
            if (finalScoreText != null)
                finalScoreText.text = $"Distance: {ScoreManager.Instance.Score}m";

            if (gameOverHighScoreText != null)
                gameOverHighScoreText.text = $"Best: {ScoreManager.Instance.HighScore}m";
        }
    }

    private void SetAllPanels(bool active)
    {
        waitingForPhonePanel?.SetActive(active);
        playingPanel?.SetActive(active);
        crashPanel?.SetActive(active);
        gameOverPanel?.SetActive(active);
    }

    // ── Button callbacks ──────────────────────────────────────────────────────

    public void OnRestartPressed() => GameManager.Instance?.RestartGame();
}
