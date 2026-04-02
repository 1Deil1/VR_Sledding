using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Pings the Glitch.com relay HTTP endpoint on game start to wake it up.
/// Glitch free tier sleeps after 5 minutes of inactivity.
/// This ensures the relay is awake before the player scans the QR code.
/// </summary>
public class RelayPinger : MonoBehaviour
{
    [SerializeField] private string relayHttpUrl = "https://your-relay.glitch.me/ping";
    [SerializeField] private float pingInterval  = 240f; // ping every 4 minutes to prevent sleep

    private void Start()
    {
        StartCoroutine(PingRoutine());
    }

    private IEnumerator PingRoutine()
    {
        while (true)
        {
            yield return SendPing();
            yield return new WaitForSeconds(pingInterval);
        }
    }

    private IEnumerator SendPing()
    {
        using var req = UnityWebRequest.Get(relayHttpUrl);
        req.timeout = 10;
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log("[RelayPinger] Relay is awake.");
        else
            Debug.LogWarning($"[RelayPinger] Ping failed: {req.error}");
    }
}
