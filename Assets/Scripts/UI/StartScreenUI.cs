using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages the Start Screen.
/// Shows QR code, connection status, and a Start button that only
/// activates once the phone controller is connected.
/// Works as a World Space Canvas for VR.
/// </summary>
public class StartScreenUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject waitingPanel;
    [SerializeField] private GameObject connectedPanel;

    [Header("Status")]
    [SerializeField] private TextMeshProUGUI relayStatusText;
    [SerializeField] private TextMeshProUGUI phoneStatusText;
    [SerializeField] private Image relayStatusDot;
    [SerializeField] private Image phoneStatusDot;

    [Header("QR Code")]
    [SerializeField] private RawImage qrCodeImage;
    [SerializeField] private TextMeshProUGUI urlText; // shows URL below QR

    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private TextMeshProUGUI startButtonText;

    [Header("Colors")]
    [SerializeField] private Color colorConnected    = new Color(0f,   1f,   0.4f);
    [SerializeField] private Color colorDisconnected = new Color(1f,   0.3f, 0.3f);
    [SerializeField] private Color colorWaiting      = new Color(1f,   0.7f, 0f);

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "Game";

    private void Start()
    {
        if (startButton != null)
            startButton.onClick.AddListener(OnStartPressed);

        UpdateUI();
    }

    private void Update()
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (RelayClient.Instance == null) return;

        bool relayOk = RelayClient.Instance.IsRelayConnected;
        bool phoneOk = RelayClient.Instance.IsPhoneConnected;

        // Relay status
        if (relayStatusText != null)
            relayStatusText.text = relayOk ? "Relay: Connected" : "Relay: Connecting...";
        if (relayStatusDot != null)
            relayStatusDot.color = relayOk ? colorConnected : colorWaiting;

        // Phone status
        if (phoneStatusText != null)
            phoneStatusText.text = phoneOk ? "Controller: Connected" : "Controller: Waiting...";
        if (phoneStatusDot != null)
            phoneStatusDot.color = phoneOk ? colorConnected : colorDisconnected;

        // Show correct panel
        if (waitingPanel != null)   waitingPanel.SetActive(!phoneOk);
        if (connectedPanel != null) connectedPanel.SetActive(phoneOk);

        // Start button
        if (startButton != null)
        {
            startButton.interactable = phoneOk;
            if (startButtonText != null)
                startButtonText.text = phoneOk ? "Start Game" : "Waiting for controller...";
        }
    }

    private void OnStartPressed()
    {
        if (RelayClient.Instance != null && RelayClient.Instance.IsPhoneConnected)
            SceneManager.LoadScene(gameSceneName);
    }
}
