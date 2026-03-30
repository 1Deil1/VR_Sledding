using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Combined HTTPS + WSS server on a single port (default 8443) for iOS support.
///
/// iOS 13+ blocks the DeviceOrientationEvent API unless the page is served
/// from a secure context (HTTPS). This server handles both in one place so
/// the user only needs to accept the self-signed certificate warning once:
///
///   Regular GET request  → serves phone_controller.html over HTTPS
///   WebSocket upgrade    → WSS connection; tilt data is fed into PhoneInputServer
///
/// SETUP: Add this component to the Managers GameObject in your scene.
///
/// iOS USER STEPS (one-time per device):
///   1. Open https://&lt;IP&gt;:8443/controller in Safari
///   2. Tap "Show Details" → "visit this website" → "Visit Website"
///   3. Enter the IP in the page and tap Connect
/// </summary>
public class IosSecureServer : MonoBehaviour
{
    [Header("Secure Server Settings")]
    [SerializeField] private int securePort = 8443;

    private TcpListener      _listener;
    private Thread           _acceptThread;
    private volatile bool    _running = false;
    private string           _pageContent;
    private X509Certificate2 _certificate;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        // Unity 6 uses UnityTLS (mbedTLS) as its TLS backend. mbedTLS is designed
        // for outgoing client connections and cannot act as a TLS server inside
        // Unity — SslStream.AuthenticateAsServer() always fails with UNITYTLS_INTERNAL_ERROR.
        //
        // SOLUTION: Use the file:// approach for iOS instead.
        // The player opens http://<IP>:8081/controller in Safari, taps
        // Share → Save to Files → Save, then opens it from the Files app.
        // file:// is a secure context on iOS so DeviceOrientationEvent works,
        // and ws:// WebSocket connections to local IPs are not blocked from file:// pages.
        //
        // iOS STEPS (one-time save, then just tap to reopen):
        //   1. Safari → http://<IP>:8081/controller
        //   2. Tap Share (box + arrow) → Save to Files → On My iPhone → Downloads → Save
        //   3. Open Files app → On My iPhone → Downloads → tap controller
        //   4. Enter IP → tap Connect & Start → Allow motion

        string ip = PhoneInputServer.Instance != null
            ? PhoneInputServer.Instance.LocalIPAddress
            : "<IP shown in game>";

        Debug.LogWarning(
            "[IosSecureServer] In-Unity HTTPS server is not supported in Unity 6 (UnityTLS limitation).\n" +
            "iPhone users — one-time setup to save the controller to your phone:\n" +
            $"  1. Open Safari on iPhone → http://{ip}:8081/download\n" +
            "     Safari will download the file automatically.\n" +
            "  2. Open the Files app → On My iPhone → Downloads\n" +
            "  3. Tap sled_controller.html → it opens in Safari as file://\n" +
            "  4. Type the IP shown in the game → tap Connect & Start → tap Allow");
    }

    private void OnDestroy()
    {
        _running = false;
        try { _listener?.Stop(); } catch { /* ignore */ }
    }

    // ── Startup ───────────────────────────────────────────────────────────────

    private void LoadPageContent()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "phone_controller.html");
        if (File.Exists(path))
        {
            _pageContent = File.ReadAllText(path, Encoding.UTF8);
        }
        else
        {
            _pageContent = "<html><body style='font-family:sans-serif;padding:2rem'>" +
                           "<h2>Controller page not found</h2>" +
                           "<p>Make sure <b>phone_controller.html</b> is in Assets/StreamingAssets/</p>" +
                           "</body></html>";
            Debug.LogWarning("[IosSecureServer] phone_controller.html not found in StreamingAssets.");
        }
    }

    private void StartSecureServer()
    {
        NetworkPortHelper.EnsurePortOpen(securePort, "VRSledGame HTTPS/WSS");

        _running  = true;
        _listener = new TcpListener(IPAddress.Any, securePort);
        _listener.Start();

        _acceptThread = new Thread(AcceptLoop) { IsBackground = true };
        _acceptThread.Start();

        string ip = PhoneInputServer.Instance?.LocalIPAddress ?? "localhost";
        Debug.Log($"[IosSecureServer] iOS secure server → https://{ip}:{securePort}/controller");
    }

    // ── Accept loop (background thread) ──────────────────────────────────────

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
                    Debug.LogWarning($"[IosSecureServer] Accept error: {e.Message}");
            }
        }
    }

    // ── Per-connection handler ────────────────────────────────────────────────

    private void HandleClient(TcpClient client)
    {
        SslStream ssl = null;
        try
        {
            ssl = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);

            // TLS 1.2 is required — iOS 13+ does not support TLS 1.0/1.1
            ssl.AuthenticateAsServer(
                _certificate,
                clientCertificateRequired: false,
                enabledSslProtocols: SslProtocols.None,  // let OS negotiate TLS version
                checkCertificateRevocation: false);

            string headers = ReadHttpHeaders(ssl);
            if (string.IsNullOrEmpty(headers)) return;

            bool isWsUpgrade =
                headers.IndexOf("Upgrade: websocket", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isWsUpgrade)
                HandleWssSession(ssl, headers);
            else
                ServeHtmlPage(ssl);
        }
        catch (Exception e)
        {
            if (_running)
            {
                string inner = e.InnerException != null ? $" | Inner: {e.InnerException.Message}" : "";
                Debug.LogWarning($"[IosSecureServer] Client error: {e.Message}{inner}");
            }
        }
        finally
        {
            ssl?.Close();
            client.Close();
        }
    }

    // ── HTTPS: serve controller page ──────────────────────────────────────────

    private void ServeHtmlPage(SslStream ssl)
    {
        byte[] body    = Encoding.UTF8.GetBytes(_pageContent);
        string headers =
            "HTTP/1.1 200 OK\r\n"                              +
            "Content-Type: text/html; charset=utf-8\r\n"       +
            $"Content-Length: {body.Length}\r\n"               +
            "Connection: close\r\n"                            +
            // Strict-Transport-Security header omitted intentionally — self-signed context
            "\r\n";

        byte[] headerBytes = Encoding.UTF8.GetBytes(headers);
        ssl.Write(headerBytes, 0, headerBytes.Length);
        ssl.Write(body, 0, body.Length);
        ssl.Flush();
    }

    // ── WSS: WebSocket over TLS ───────────────────────────────────────────────

    private void HandleWssSession(SslStream ssl, string requestHeaders)
    {
        if (!PerformWsHandshake(ssl, requestHeaders))
        {
            Debug.LogWarning("[IosSecureServer] WSS handshake failed.");
            return;
        }

        PhoneInputServer.Instance?.SetConnected(true);
        Debug.Log("[IosSecureServer] iOS phone connected via WSS.");

        try
        {
            while (_running)
            {
                string message = ReadWebSocketFrame(ssl);
                if (message == null) break;     // Close frame or EOF
                if (message.Length > 0)
                    ParseTiltMessage(message);
            }
        }
        finally
        {
            PhoneInputServer.Instance?.SetConnected(false);
            Debug.Log("[IosSecureServer] iOS phone disconnected.");
        }
    }

    // ── RFC 6455 handshake ────────────────────────────────────────────────────

    private bool PerformWsHandshake(SslStream ssl, string request)
    {
        const string keyHeader = "Sec-WebSocket-Key: ";
        int idx = request.IndexOf(keyHeader, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return false;

        idx += keyHeader.Length;
        int end = request.IndexOf("\r\n", idx, StringComparison.Ordinal);
        if (end < 0) return false;

        string key       = request.Substring(idx, end - idx).Trim();
        string acceptKey = ComputeAcceptKey(key);

        string response =
            "HTTP/1.1 101 Switching Protocols\r\n" +
            "Upgrade: websocket\r\n"               +
            "Connection: Upgrade\r\n"              +
            $"Sec-WebSocket-Accept: {acceptKey}\r\n\r\n";

        byte[] bytes = Encoding.UTF8.GetBytes(response);
        ssl.Write(bytes, 0, bytes.Length);
        ssl.Flush();
        return true;
    }

    private static string ComputeAcceptKey(string key)
    {
        const string magic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        byte[] hash = SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(key + magic));
        return Convert.ToBase64String(hash);
    }

    // ── RFC 6455 frame reader ─────────────────────────────────────────────────

    private string ReadWebSocketFrame(Stream stream)
    {
        int b0 = stream.ReadByte();
        if (b0 < 0) return null;

        int opcode = b0 & 0x0F;
        if (opcode == 8) return null;   // Close frame

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
            ReadExact(stream, 8);
            return "";
        }

        byte[] mask    = masked ? ReadExact(stream, 4) : null;
        if (masked && mask == null) return null;

        byte[] payload = ReadExact(stream, payloadLen);
        if (payload == null) return null;

        if (masked)
            for (int i = 0; i < payloadLen; i++)
                payload[i] ^= mask[i % 4];

        return opcode == 1 ? Encoding.UTF8.GetString(payload) : "";
    }

    // ── HTTP header reader (byte-by-byte until \r\n\r\n) ─────────────────────

    private static string ReadHttpHeaders(Stream stream)
    {
        var  sb  = new StringBuilder();
        var  buf = new byte[1];
        int  safetyLimit = 8192;

        while (sb.Length < safetyLimit)
        {
            int read = stream.Read(buf, 0, 1);
            if (read <= 0) break;

            sb.Append((char)buf[0]);

            if (sb.Length >= 4 &&
                sb[sb.Length - 4] == '\r' && sb[sb.Length - 3] == '\n' &&
                sb[sb.Length - 2] == '\r' && sb[sb.Length - 1] == '\n')
                break;
        }

        return sb.ToString();
    }

    private static byte[] ReadExact(Stream stream, int count)
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

    // ── JSON parse ────────────────────────────────────────────────────────────

    [Serializable]
    private struct PhoneData { public float pitch; public float roll; }

    private static void ParseTiltMessage(string json)
    {
        try
        {
            PhoneData d = JsonUtility.FromJson<PhoneData>(json);
            PhoneInputServer.Instance?.SetRawTilt(d.pitch, d.roll);
        }
        catch { /* ignore malformed frames */ }
    }
}
