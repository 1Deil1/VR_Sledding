using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;

/// <summary>
/// Manages a self-signed TLS certificate for IosSecureServer.
///
/// WHY PFX AND NOT X509STORE:
/// Mono's X509Store does NOT map to the Windows certificate store — it has its own
/// separate store, so loading by thumbprint/subject always returns nothing even when
/// the cert was just installed by PowerShell. Loading directly from a PFX file is
/// the only reliable approach inside Unity/Mono on Windows.
///
/// The PFX is written to Application.persistentDataPath and reused across sessions.
/// Delete vrsled_server.pfx to force regeneration.
/// </summary>
public static class CertificateManager
{
    private const string PfxFileName = "vrsled_server.pfx";
    private const string PfxPassword = "VRSledGame2026";

    private static X509Certificate2 _certificate;

    public static X509Certificate2 GetCertificate()
    {
        if (_certificate != null) return _certificate;

        string pfxPath = Path.Combine(Application.persistentDataPath, PfxFileName);
        Debug.Log("[CertificateManager] PFX path: " + pfxPath);

        if (!File.Exists(pfxPath))
        {
            Debug.Log("[CertificateManager] PFX not found, generating via PowerShell...");
            GeneratePfxWithPowerShell(pfxPath);
        }

        if (!File.Exists(pfxPath))
        {
            Debug.LogError("[CertificateManager] PFX still missing after generation. " +
                           "Check the PowerShell stdout/stderr log lines above.");
            return null;
        }

        try
        {
            // UserKeySet is required — EphemeralKeySet prevents SslStream from
            // accessing the private key on Mono. PersistKeySet keeps it across calls.
            _certificate = new X509Certificate2(
                pfxPath, PfxPassword,
                X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

            Debug.Log("[CertificateManager] Loaded PFX. Subject=" + _certificate.Subject +
                      "  Thumbprint=" + _certificate.Thumbprint +
                      "  HasPrivateKey=" + _certificate.HasPrivateKey);
            return _certificate;
        }
        catch (Exception e)
        {
            Debug.LogError("[CertificateManager] Failed to load PFX: " + e.Message);
            try { File.Delete(pfxPath); } catch { /* let it regenerate next run */ }
            return null;
        }
    }

    /// <summary>Delete the PFX so it is regenerated on next GetCertificate() call.</summary>
    public static void RegenerateCertificate()
    {
        _certificate = null;
        string pfxPath = Path.Combine(Application.persistentDataPath, PfxFileName);
        if (File.Exists(pfxPath)) { File.Delete(pfxPath); }
        Debug.Log("[CertificateManager] PFX deleted — will regenerate on next use.");
    }

    // ── PFX generation via PowerShell (Windows 10/11 built-in) ───────────────

    private static void GeneratePfxWithPowerShell(string pfxPath)
    {
        // Use forward slashes — PowerShell accepts them and it avoids C# escape issues
        string pfxPathPs   = pfxPath.Replace('\\', '/');
        string tempScript  = Path.Combine(Path.GetTempPath(), "gen_vrsled_cert.ps1");

        // One statement per line, no backtick continuations.
        string nl = "\r\n";
        string script =
            "$ErrorActionPreference = 'Stop'" + nl +
            "try {" + nl +
            "  $store = 'Cert:\\CurrentUser\\My'" + nl +
            "  Get-ChildItem $store | Where-Object { $_.Subject -like '*VRSledGame*' } | Remove-Item -Force -ErrorAction SilentlyContinue" + nl +
            "  $cert = New-SelfSignedCertificate -DnsName 'VRSledGame' -CertStoreLocation $store -NotAfter (Get-Date).AddYears(2) -KeyExportPolicy Exportable -KeySpec KeyExchange" + nl +
            "  $pw = ConvertTo-SecureString -String '" + PfxPassword + "' -Force -AsPlainText" + nl +
            "  Export-PfxCertificate -Cert $cert.PSPath -FilePath '" + pfxPathPs + "' -Password $pw -CryptoAlgorithmOption TripleDES_SHA1 | Out-Null" + nl +
            "  Write-Output ('CERT_OK:' + $cert.Thumbprint)" + nl +
            "} catch {" + nl +
            "  Write-Output ('CERT_FAIL:' + $_.Exception.Message)" + nl +
            "}";

        try
        {
            File.WriteAllText(tempScript, script, System.Text.Encoding.UTF8);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName               = "powershell.exe",
                Arguments              = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + tempScript + "\"",
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true
            };

            System.Diagnostics.Process proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) { Debug.LogError("[CertificateManager] Could not start PowerShell."); return; }

            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit(30_000);
            proc.Dispose();

            Debug.Log("[CertificateManager] PS stdout: " + stdout.Trim());
            if (!string.IsNullOrWhiteSpace(stderr))
                Debug.LogWarning("[CertificateManager] PS stderr: " + stderr.Trim());

            if (stdout.Contains("CERT_OK"))
                Debug.Log("[CertificateManager] PFX written. File exists: " + File.Exists(pfxPath));
            else
                Debug.LogError("[CertificateManager] PowerShell did not output CERT_OK.");
        }
        catch (Exception e)
        {
            Debug.LogError("[CertificateManager] Cannot run PowerShell: " + e.Message);
        }
        finally
        {
            try { File.Delete(tempScript); } catch { /* ignore */ }
        }
    }
}
