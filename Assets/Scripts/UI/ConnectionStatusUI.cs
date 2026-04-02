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
    [Header("Text References")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI ipText;

    [Header("Status Colors")]
    [SerializeField] private Color connectedColor    = Color.green;
    [SerializeField] private Color disconnectedColor = Color.red;
    [SerializeField] private Color waitingColor      = Color.yellow;

    private bool _lastConnectedState = false;

    private void Start()
    {
        RefreshIPDisplay();
        SetStatus(false);
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

    private void SetStatus(bool connected)
    {
        if (statusText == null) return;

        statusText.text  = connected ? "● Phone Connected" : "○ Waiting for phone…";
        statusText.color = connected ? connectedColor : waitingColor;
    }
}
