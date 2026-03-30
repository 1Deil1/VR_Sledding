using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Opens the inbound Windows Firewall ports that the phone controller needs.
/// Called automatically at startup by IosSecureServer and PhoneInputServer.
///
/// Requires the process to have Administrator privileges OR for the rules to
/// already exist. If it fails due to permissions, it logs the exact netsh
/// commands to run once manually in an elevated terminal.
/// </summary>
public static class NetworkPortHelper
{
    /// <summary>
    /// Ensures inbound firewall rules exist for the given port.
    /// Safe to call multiple times — does nothing if the rule already exists.
    /// </summary>
    public static void EnsurePortOpen(int port, string ruleName)
    {
        // Check / add TCP rule
        if (!RuleExists(ruleName))
            AddRule(ruleName, port);
    }

    // ── Rule checks ───────────────────────────────────────────────────────────

    private static bool RuleExists(string name)
    {
        string output = RunNetsh($"advfirewall firewall show rule name=\"{name}\"");
        return output != null && output.Contains("Rule Name:");
    }

    private static void AddRule(string name, int port)
    {
        string args =
            $"advfirewall firewall add rule " +
            $"name=\"{name}\" " +
            $"dir=in " +
            $"action=allow " +
            $"protocol=TCP " +
            $"localport={port} " +
            $"profile=private,domain";

        string result = RunNetsh(args);

        if (result != null && result.Contains("Ok."))
        {
            Debug.Log($"[NetworkPortHelper] Opened port {port} in Windows Firewall (rule: {name}).");
        }
        else
        {
            // Non-fatal — log the manual steps the user can run once in an elevated terminal
            Debug.LogWarning(
                $"[NetworkPortHelper] Could not automatically open port {port}. " +
                $"If your phone can't connect, run this ONCE in PowerShell as Administrator:\n\n" +
                $"  netsh advfirewall firewall add rule name=\"{name}\" dir=in action=allow protocol=TCP localport={port} profile=private,domain\n\n" +
                $"Or simply run Unity as Administrator once — the rule will be added automatically.");
        }
    }

    // ── netsh runner ──────────────────────────────────────────────────────────

    private static string RunNetsh(string arguments)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName               = "netsh.exe",
                Arguments              = arguments,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true
            };

            using System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi);
            if (p == null) return null;

            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5_000);
            return output;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NetworkPortHelper] netsh call failed: {e.Message}");
            return null;
        }
    }
}
