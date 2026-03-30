using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Runs a lightweight WebSocket server (RFC 6455) that receives tilt data from the player's phone.
/// No external packages required — uses only built-in .NET sockets.
/// Pitch and Roll are exposed as smoothed public properties for SledController to read.
/// </summary>
public class PhoneInputServer : MonoBehaviour
{
    public static PhoneInputServer Instance { get; private set; }

    [Header("Server Settings")]
    [SerializeField] private int port = 8080;
    [SerializeField] private bool logMessages = false;

    [Header("Input Smoothing")]
    [SerializeField, Range(1f, 20f)] private float smoothingSpeed = 4f;

    [Header("Input Clamping")]
    [Tooltip("Raw pitch (beta) is clamped to this range before smoothing. Keeps slight tilts from maxing out.")]
    [SerializeField] private float maxPitchInput = 40f;
    [Tooltip("Raw roll (gamma) is clamped to this range before smoothing.")]
    [SerializeField] private float maxRollInput  = 40f;

    /// <summary>Forward/back tilt in degrees (smoothed, clamped). Positive = lean forward.</summary>
    public float Pitch { get; private set; } = 0f;

    /// <summary>Left/right tilt in degrees (smoothed, clamped). Positive = lean right.</summary>
    public float Roll  { get; private set; } = 0f;

    public bool IsConnected { get; set; } = false;
    public string LocalIPAddress { get; private set; } = "";

    private float _rawPitch = 0f;
    private float _rawRoll  = 0f;

    private TcpListener _listener;
    private Thread _serverThread;
    private volatile bool _running = false;

    private readonly ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();

    // Matches the JSON structure sent by phone_controller.html
    [Serializable]
    private struct PhoneData
    {
        public float pitch;
        public float roll;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LocalIPAddress = GetLocalIPAddress();
    }

    private void Start()
    {
        StartServer();
    }

    private void Update()
    {
        // Smooth raw values on the main thread
        Pitch = Mathf.Lerp(Pitch, _rawPitch, Time.deltaTime * smoothingSpeed);
        Roll  = Mathf.Lerp(Roll,  _rawRoll,  Time.deltaTime * smoothingSpeed);

        // Drain the thread-safe message queue
        while (_messageQueue.TryDequeue(out string json))
            ParseMessage(json);
    }

    private void StartServer()
    {        NetworkPortHelper.EnsurePortOpen(port, "VRSledGame WebSocket");
        _running = true;
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();

        _serverThread = new Thread(AcceptLoop) { IsBackground = true };
        _serverThread.Start();

        Debug.Log($"[PhoneServer] WebSocket server started — ws://{LocalIPAddress}:{port}");
    }

    // ── Background threads ────────────────────────────────────────────────────

    private void AcceptLoop()
    {
        while (_running)
        {
            try
            {
                TcpClient client = _listener.AcceptTcpClient();
                Thread t = new Thread(() => HandleClient(client)) { IsBackground = true };
                t.Start();
            }
            catch (Exception e)
            {
                if (_running)
                    Debug.LogWarning($"[PhoneServer] Accept error: {e.Message}");
            }
        }
    }

    private void HandleClient(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        try
        {
            if (!PerformHandshake(stream))
            {
                Debug.LogWarning("[PhoneServer] WebSocket handshake failed.");
                return;
            }

            IsConnected = true;
            Debug.Log("[PhoneServer] Phone connected.");

            while (_running && client.Connected)
            {
                string msg = ReadWebSocketFrame(stream);
                if (msg == null) break;          // Close frame or EOF
                if (msg.Length > 0)
                    _messageQueue.Enqueue(msg);
            }
        }
        catch (Exception e)
        {
            if (_running)
                Debug.LogWarning($"[PhoneServer] Client error: {e.Message}");
        }
        finally
        {
            IsConnected = false;
            Debug.Log("[PhoneServer] Phone disconnected.");
            stream.Close();
            client.Close();
        }
    }

    // ── WebSocket handshake (RFC 6455) ────────────────────────────────────────

    private bool PerformHandshake(NetworkStream stream)
    {
        byte[] buffer = new byte[4096];
        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        const string keyHeader = "Sec-WebSocket-Key: ";
        int keyIndex = request.IndexOf(keyHeader, StringComparison.Ordinal);
        if (keyIndex < 0) return false;

        keyIndex += keyHeader.Length;
        int keyEnd = request.IndexOf("\r\n", keyIndex, StringComparison.Ordinal);
        if (keyEnd < 0) return false;

        string wsKey     = request.Substring(keyIndex, keyEnd - keyIndex).Trim();
        string acceptKey = ComputeAcceptKey(wsKey);

        string response =
            "HTTP/1.1 101 Switching Protocols\r\n" +
            "Upgrade: websocket\r\n"              +
            "Connection: Upgrade\r\n"             +
            $"Sec-WebSocket-Accept: {acceptKey}\r\n\r\n";

        byte[] responseBytes = Encoding.UTF8.GetBytes(response);
        stream.Write(responseBytes, 0, responseBytes.Length);
        return true;
    }

    private static string ComputeAcceptKey(string wsKey)
    {
        const string magic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        byte[] hash = SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(wsKey + magic));
        return Convert.ToBase64String(hash);
    }

    // ── WebSocket frame reader (RFC 6455) ────────────────────────────────────

    /// <summary>
    /// Reads one WebSocket frame. Returns null on close/EOF, empty string on non-text frame,
    /// or the decoded UTF-8 payload for a text frame.
    /// Client frames are always masked per spec.
    /// </summary>
    private string ReadWebSocketFrame(NetworkStream stream)
    {
        int b0 = stream.ReadByte();
        if (b0 < 0) return null;

        int opcode = b0 & 0x0F;
        if (opcode == 8) return null; // Close frame

        int b1 = stream.ReadByte();
        if (b1 < 0) return null;

        bool masked     = (b1 & 0x80) != 0;
        int  payloadLen = b1 & 0x7F;

        if (payloadLen == 126)
        {
            byte[] ext = ReadExact(stream, 2);
            if (ext == null) return null;
            payloadLen = (ext[0] << 8) | ext[1];
        }
        else if (payloadLen == 127)
        {
            // 64-bit length frames not expected from a phone controller; skip
            ReadExact(stream, 8);
            return "";
        }

        byte[] mask = masked ? ReadExact(stream, 4) : null;
        if (masked && mask == null) return null;

        byte[] payload = ReadExact(stream, payloadLen);
        if (payload == null) return null;

        if (masked)
            for (int i = 0; i < payloadLen; i++)
                payload[i] ^= mask[i % 4];

        return opcode == 1 ? Encoding.UTF8.GetString(payload) : ""; // 1 = text frame
    }

    private static byte[] ReadExact(NetworkStream stream, int count)
    {
        byte[] buf   = new byte[count];
        int    total = 0;
        while (total < count)
        {
            int read = stream.Read(buf, total, count - total);
            if (read <= 0) return null;
            total += read;
        }
        return buf;
    }

    // ── External input (called by IosSecureServer for WSS connections) ──────

    /// <summary>
    /// Allows IosSecureServer (WSS) to feed raw tilt data from an iOS phone.
    /// Thread-safe: individual float writes are atomic on all Unity platforms.
    /// </summary>
    public void SetRawTilt(float pitch, float roll)
    {
        _rawPitch = pitch;
        _rawRoll  = roll;
    }

    /// <summary>Allows IosSecureServer to update the connection status flag.</summary>
    public void SetConnected(bool connected)
    {
        IsConnected = connected;
    }

    // ── JSON parsing ─────────────────────────────────────────────────────────

    private void ParseMessage(string json)
    {
        try
        {
            if (logMessages) Debug.Log($"[PhoneServer] Raw: {json}");
            PhoneData data = JsonUtility.FromJson<PhoneData>(json);
            // Clamp to a comfortable tilt range so tiny phone movements
            // don't immediately max out the controller input.
            _rawPitch = Mathf.Clamp(data.pitch, -maxPitchInput, maxPitchInput);
            _rawRoll  = Mathf.Clamp(data.roll,  -maxRollInput,  maxRollInput);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PhoneServer] Failed to parse message: {e.Message}");
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        _running = false;
        _listener?.Stop();
    }

    private static string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
        }
        catch { /* fall through */ }
        return "127.0.0.1";
    }
}
