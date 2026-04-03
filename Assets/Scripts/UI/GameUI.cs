using UnityEngine;
using UnityEngine.UI;
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
    [Header("Style")]
    [SerializeField] private UIStyleConfig styleConfig;

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

    [Header("Buttons")]
    [SerializeField] private Button restartButton;

    [Header("References")]
    [SerializeField] private SledController sledController;

    private void Start()
    {
        // Auto-find sled controller if not assigned in Inspector
        if (sledController == null)
            sledController = FindAnyObjectByType<SledController>();

        // Wire up restart button in code (World Space sub-Canvas can break Inspector OnClick)
        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartPressed);

        ShowWaitingForPhoneUI();

        // Show both URLs so the player knows which one to open on their phone
        if (RelayClient.Instance != null && ipAddressText != null)
        {
            ipAddressText.text =
                "Scan the QR code on the Start Screen\n" +
                "to connect your phone controller.";
        }
    }

    /// <summary>Applies panel background color from styleConfig to a single panel.</summary>
    private void StylePanel(GameObject panel)
    {
        if (styleConfig == null || panel == null) return;
        UIStyleApplier.ApplyPanelColor(panel, styleConfig.panelColor);
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

    /// <summary>
    /// Apply text color AFTER TMP has rebuilt its mesh in Update().
    /// This is the only reliable way to override TMP text colors at runtime.
    /// </summary>
    private void LateUpdate()
    {
        if (styleConfig == null) return;
        ApplyTextIfActive(waitingForPhonePanel);
        ApplyTextIfActive(playingPanel);
        ApplyTextIfActive(crashPanel);
        ApplyTextIfActive(gameOverPanel);
    }

    private void ApplyTextIfActive(GameObject panel)
    {
        if (panel != null && panel.activeSelf)
            UIStyleApplier.ApplyTextColorToAll(panel, styleConfig.textColor);
    }

    // ── Panel switching ───────────────────────────────────────────────────────

    public void ShowWaitingForPhoneUI()
    {
        SetAllPanels(false);
        waitingForPhonePanel?.SetActive(true);
        StylePanel(waitingForPhonePanel);
    }

    public void ShowPlayingUI()
    {
        SetAllPanels(false);
        playingPanel?.SetActive(true);
        StylePanel(playingPanel);

        if (highScoreText != null && ScoreManager.Instance != null)
            highScoreText.text = $"Best: {ScoreManager.Instance.HighScore}m";
    }

    public void ShowCrashUI()
    {
        // Overlay the crash panel on top of the playing panel
        crashPanel?.SetActive(true);
        StylePanel(crashPanel);
    }

    public void ShowGameOverUI()
    {
        SetAllPanels(false);
        gameOverPanel?.SetActive(true);
        StylePanel(gameOverPanel);
        UIStyleApplier.ApplyButtonColors(restartButton, styleConfig);

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

    public void OnRestartPressed()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("StartScreen");
    }
}
