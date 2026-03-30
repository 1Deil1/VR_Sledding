using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Serves phone_controller.html over HTTP so the player can open the sled
/// controller on their phone without any separate web server.
///
/// Access URL: http://&lt;local_ip&gt;:8081/controller
///
/// WINDOWS NOTE: Binding to http://*:8081/ requires either running Unity as
/// Administrator, or running once in an elevated prompt:
///   netsh http add urlacl url=http://*:8081/ user=Everyone
/// If that fails, the server falls back to localhost only (useful in Editor).
/// </summary>
public class PhonePageServer : MonoBehaviour
{
    [Header("Server Settings")]
    [SerializeField] private int httpPort = 8081;

    private HttpListener _listener;
    private Thread       _serverThread;
    private volatile bool _running = false;
    private string _pageContent;

    private void Start()
    {
        LoadPageContent();
        StartHttpServer();
    }

    // ── Page content ──────────────────────────────────────────────────────────

    private void LoadPageContent()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "phone_controller.html");

        if (File.Exists(path))
        {
            _pageContent = File.ReadAllText(path, Encoding.UTF8);
        }
        else
        {
            _pageContent =
                "<html><body style='font-family:sans-serif;padding:2rem'>"     +
                "<h2>Controller page not found</h2>"                           +
                "<p>Make sure <b>phone_controller.html</b> exists inside "     +
                "<b>Assets/StreamingAssets/</b>.</p></body></html>";
            Debug.LogWarning("[PageServer] phone_controller.html not found in StreamingAssets.");
        }
    }

    // ── HTTP server ───────────────────────────────────────────────────────────

    private void StartHttpServer()
    {
        _running  = true;
        _listener = new HttpListener();

        // Try network-accessible prefix first, fall back to localhost
        bool started = TryStartListener($"http://*:{httpPort}/");
        if (!started)
            started = TryStartListener($"http://localhost:{httpPort}/");

        if (!started)
        {
            Debug.LogError("[PageServer] Could not start HTTP server on any address.");
            return;
        }

        _serverThread = new Thread(ServeLoop) { IsBackground = true };
        _serverThread.Start();

        string ip = PhoneInputServer.Instance != null
            ? PhoneInputServer.Instance.LocalIPAddress
            : "localhost";
        Debug.Log($"[PageServer] Android → http://{ip}:{httpPort}/controller");
        Debug.Log($"[PageServer] iPhone  → http://{ip}:{httpPort}/download  (tap to save to Files, then open from Files app)");
    }

    private bool TryStartListener(string prefix)
    {
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _listener.Start();
            return true;
        }
        catch (HttpListenerException ex)
        {
            Debug.LogWarning($"[PageServer] Cannot bind to {prefix}: {ex.Message}");
            return false;
        }
    }

    // ── Request handler (background thread) ──────────────────────────────────

    private void ServeLoop()
    {
        while (_running)
        {
            try
            {
                HttpListenerContext ctx  = _listener.GetContext();
                string              path = ctx.Request.Url.AbsolutePath;

                if (path == "/" || path == "/controller" || path == "/controller.html")
                {
                    // Normal page view (Android / desktop browser)
                    byte[] data = Encoding.UTF8.GetBytes(_pageContent);
                    ctx.Response.StatusCode      = 200;
                    ctx.Response.ContentType     = "text/html; charset=utf-8";
                    ctx.Response.ContentLength64 = data.Length;
                    ctx.Response.OutputStream.Write(data, 0, data.Length);
                }
                else if (path == "/download" || path == "/download.html")
                {
                    // iPhone download route — Content-Disposition forces Safari to save the
                    // file to Files app. User then opens it as file:// (secure context).
                    byte[] data = Encoding.UTF8.GetBytes(_pageContent);
                    ctx.Response.StatusCode      = 200;
                    ctx.Response.ContentType     = "text/html; charset=utf-8";
                    ctx.Response.ContentLength64 = data.Length;
                    ctx.Response.AddHeader("Content-Disposition", "attachment; filename=\"sled_controller.html\"");
                    ctx.Response.OutputStream.Write(data, 0, data.Length);
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                }

                ctx.Response.Close();
            }
            catch (Exception e)
            {
                if (_running)
                    Debug.LogWarning($"[PageServer] Error: {e.Message}");
            }
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        _running = false;
        try { _listener?.Stop(); } catch { /* ignore */ }
    }
}
