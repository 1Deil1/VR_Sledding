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
    [Header("Style")]
    [SerializeField] private UIStyleConfig styleConfig;

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

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "Game";

    // Resolved at runtime from styleConfig (or fallback defaults)
    private Color colorConnected;
    private Color colorDisconnected;
    private Color colorWaiting;

    private void Start()
    {
        ResolveColors();

        if (startButton != null)
            startButton.onClick.AddListener(OnStartPressed);

        UpdateUI();
    }

    private void ResolveColors()
    {
        if (styleConfig != null)
        {
            colorConnected    = styleConfig.connectedColor;
            colorDisconnected = styleConfig.disconnectedColor;
            colorWaiting      = styleConfig.waitingColor;
        }
        else
        {
            colorConnected    = new Color(0f,   1f,   0.4f);
            colorDisconnected = new Color(1f,   0.3f, 0.3f);
            colorWaiting      = new Color(1f,   0.7f, 0f);
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
        UpdateUI();
    }

    /// <summary>
    /// Apply text color AFTER TMP has rebuilt its mesh in Update().
    /// </summary>
    private void LateUpdate()
    {
        if (styleConfig == null) return;
        if (waitingPanel != null && waitingPanel.activeSelf)
            UIStyleApplier.ApplyTextColorToAll(waitingPanel, styleConfig.textColor);
        if (connectedPanel != null && connectedPanel.activeSelf)
            UIStyleApplier.ApplyTextColorToAll(connectedPanel, styleConfig.textColor);
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

        // Show correct panel & apply style after activation
        if (waitingPanel != null)
        {
            waitingPanel.SetActive(!phoneOk);
            if (!phoneOk) StylePanel(waitingPanel);
        }
        if (connectedPanel != null)
        {
            connectedPanel.SetActive(phoneOk);
            if (phoneOk) StylePanel(connectedPanel);
        }

        // Start button
        if (startButton != null)
        {
            startButton.interactable = phoneOk;
            UIStyleApplier.ApplyButtonColors(startButton, styleConfig);
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
