using UnityEngine;
using TMPro;

/// <summary>
/// Displays the current WebSocket connection status and the local IP address
/// the player needs to enter on their phone.
///
/// Attach to any UI GameObject. Wire up the TextMeshPro references in the Inspector.
/// This component is optional — GameUI.cs also shows the IP on the waiting panel.
/// </summary>
public class ConnectionStatusUI : MonoBehaviour
{
    [Header("Style")]
    [SerializeField] private UIStyleConfig styleConfig;

    [Header("Text References")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI ipText;

    // Resolved at runtime from styleConfig (or fallback defaults)
    private Color connectedColor;
    private Color disconnectedColor;
    private Color waitingColor;

    private bool _lastConnectedState = false;

    private void Start()
    {
        ResolveColors();
        RefreshIPDisplay();
        SetStatus(false);
    }

    private void ResolveColors()
    {
        if (styleConfig != null)
        {
            connectedColor    = styleConfig.connectedColor;
            disconnectedColor = styleConfig.disconnectedColor;
            waitingColor      = styleConfig.waitingColor;
        }
        else
        {
            connectedColor    = Color.green;
            disconnectedColor = Color.red;
            waitingColor      = Color.yellow;
        }
    }

    private void Update()
    {
        if (RelayClient.Instance == null) return;

        bool connected = RelayClient.Instance.IsPhoneConnected;
        if (connected != _lastConnectedState)
        {
            _lastConnectedState = connected;
            SetStatus(connected);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RefreshIPDisplay()
    {
        if (ipText == null || RelayClient.Instance == null) return;

        string url = RelayClient.Instance.RelayUrl;
        ipText.text =
            $"Relay Server\n<b>{url}</b>\n\n" +
            $"Scan the QR code to connect";
    }

    /// <summary>
    /// Apply text color after TMP rebuilds its mesh in Update().
    /// </summary>
    private void LateUpdate()
    {
        if (styleConfig != null && ipText != null)
            UIStyleApplier.ApplyTextColor(ipText, styleConfig.textColor);
    }

    private void SetStatus(bool connected)
    {
        if (statusText == null) return;

        statusText.text  = connected ? "● Phone Connected" : "○ Waiting for phone…";
        statusText.color = connected ? connectedColor : waitingColor;
    }
}
