using UnityEngine;
using UnityEngine.UI;
using ZXing;
using ZXing.QrCode;

/// <summary>
/// Generates a QR code texture containing the full controller URL.
/// The URL includes the relay server address as a parameter so the
/// phone auto-connects without the player typing anything.
/// </summary>
public class QRCodeGenerator : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string githubPagesUrl = "https://yourusername.github.io/sled-controller/";
    [SerializeField] private string relayWssUrl    = "wss://your-relay.glitch.me";
    [SerializeField] private int textureSize = 256;

    [Header("UI")]
    [SerializeField] private RawImage qrCodeImage;

    private void Start()
    {
        GenerateQRCode();
    }

    public void GenerateQRCode()
    {
        string fullUrl = $"{githubPagesUrl}?relay={relayWssUrl}";
        Debug.Log($"[QRCode] Generating for: {fullUrl}");

        Texture2D tex = GenerateQRTexture(fullUrl, textureSize);
        if (qrCodeImage != null)
            qrCodeImage.texture = tex;
    }

    private Texture2D GenerateQRTexture(string content, int size)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions
            {
                Width  = size,
                Height = size,
                Margin = 1,
                ErrorCorrection = ZXing.QrCode.Internal.ErrorCorrectionLevel.M
            }
        };

        var pixelData = writer.Write(content);
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        Color32[] pixels = new Color32[pixelData.Pixels.Length / 4];
        for (int i = 0; i < pixels.Length; i++)
        {
            int idx = i * 4;
            pixels[i] = new Color32(
                pixelData.Pixels[idx],
                pixelData.Pixels[idx + 1],
                pixelData.Pixels[idx + 2],
                pixelData.Pixels[idx + 3]
            );
        }

        // QR codes are black on white — flip Y for Unity texture coords
        Color32[] flipped = new Color32[pixels.Length];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                flipped[y * size + x] = pixels[(size - 1 - y) * size + x];

        tex.SetPixels32(flipped);
        tex.Apply();
        return tex;
    }
}
