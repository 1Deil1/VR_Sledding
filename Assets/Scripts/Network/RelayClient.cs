using System;
using System.Collections;
using UnityEngine;
using NativeWebSocket;

/// <summary>
/// Connects Unity to the cloud relay server as a WebSocket CLIENT.
/// Receives tilt data from the phone controller via the relay.
/// No local server, no firewall issues, works on any network.
/// </summary>
public class RelayClient : MonoBehaviour
{
    public static RelayClient Instance { get; private set; }

    [Header("Relay Settings")]
    [SerializeField] private string relayUrl = "wss://your-relay.glitch.me";

    [Header("Input Smoothing")]
    [SerializeField, Range(1f, 20f)] private float smoothingSpeed = 8f;

    [Header("Reconnection")]
    [SerializeField] private float reconnectDelay = 3f;
    [SerializeField] private int maxReconnectAttempts = 10;

    // Public tilt values (smoothed)
    public float Pitch { get; private set; } = 0f;
    public float Roll  { get; private set; } = 0f;
    public bool IsPhoneConnected { get; private set; } = false;
    public bool IsRelayConnected { get; private set; } = false;
    public string RelayUrl => relayUrl;

    private float _rawPitch = 0f;
    private float _rawRoll  = 0f;
    private WebSocket _ws;
    private int _reconnectAttempts = 0;
    private bool _quitting = false;

    [Serializable]
    private struct TiltData  { public float pitch; public float roll; }
    [Serializable]
    private struct StatusMsg { public string type; public bool gameConnected; public bool phoneConnected; }
    [Serializable]
    private struct RegisterMsg { public string type; public string role; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start() => StartCoroutine(ConnectRoutine());

    private void Update()
    {
        Pitch = Mathf.Lerp(Pitch, _rawPitch, Time.deltaTime * smoothingSpeed);
        Roll  = Mathf.Lerp(Roll,  _rawRoll,  Time.deltaTime * smoothingSpeed);
#if !UNITY_WEBGL || UNITY_EDITOR
        _ws?.DispatchMessageQueue();
#endif
    }

    private IEnumerator ConnectRoutine()
    {
        while (!_quitting && _reconnectAttempts < maxReconnectAttempts)
        {
            yield return Connect();

            // Stay here while the connection is alive
            while (!_quitting && _ws != null && _ws.State == WebSocketState.Open)
                yield return null;

            if (_quitting) yield break;
            _reconnectAttempts++;
            Debug.Log($"[RelayClient] Reconnecting in {reconnectDelay}s... (attempt {_reconnectAttempts})");
            yield return new WaitForSeconds(reconnectDelay);
        }
    }

    private IEnumerator Connect()
    {
        Debug.Log($"[RelayClient] Connecting to {relayUrl}");
        _ws = new WebSocket(relayUrl);

        _ws.OnOpen += () =>
        {
            IsRelayConnected = true;
            _reconnectAttempts = 0;
            Debug.Log("[RelayClient] Connected to relay.");
            // Register as game client
            string reg = JsonUtility.ToJson(new RegisterMsg { type = "register", role = "game" });
            _ws.SendText(reg);
        };

        _ws.OnMessage += OnMessage;

        _ws.OnClose += (code) =>
        {
            IsRelayConnected = false;
            IsPhoneConnected = false;
            Debug.Log($"[RelayClient] Disconnected. Code: {code}");
        };

        _ws.OnError += (err) => Debug.LogWarning($"[RelayClient] Error: {err}");

        yield return _ws.Connect();
    }

    private void OnMessage(byte[] bytes)
    {
        try
        {
            string json = System.Text.Encoding.UTF8.GetString(bytes);

            // Check if it is a status message
            if (json.Contains("\"type\""))
            {
                StatusMsg status = JsonUtility.FromJson<StatusMsg>(json);
                if (status.type == "status")
                {
                    IsPhoneConnected = status.phoneConnected;
                    return;
                }
            }

            // Otherwise it is tilt data
            TiltData data = JsonUtility.FromJson<TiltData>(json);
            _rawPitch = data.pitch;
            _rawRoll  = data.roll;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RelayClient] Parse error: {e.Message}");
        }
    }

    public void SetRelayUrl(string url)
    {
        relayUrl = url;
    }

    private void OnApplicationQuit()
    {
        _quitting = true;
        _ws?.Close();
    }
}
