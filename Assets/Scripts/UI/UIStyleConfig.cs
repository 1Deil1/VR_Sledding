using UnityEngine;

/// <summary>
/// Centralised look-and-feel settings for every UI panel in the game.
/// Create via Assets → Create → VR Sledding → UI Style Config,
/// then drag the asset into the matching field on GameUI, StartScreenUI,
/// and ConnectionStatusUI.
/// </summary>
[CreateAssetMenu(fileName = "UIStyleConfig", menuName = "VR Sledding/UI Style Config")]
public class UIStyleConfig : ScriptableObject
{
    // ── Panel background ─────────────────────────────────────────────────────

    [Header("Panel Background (applies to every panel)")]
    [Tooltip("Background color and transparency for all UI panels")]
    public Color panelColor = new Color(0.1f, 0.1f, 0.15f, 0.85f);

    // ── Text color ───────────────────────────────────────────────────────────

    [Header("Text Color (applies to all text)")]
    [Tooltip("Color used for all UI text elements")]
    public Color textColor = Color.white;

    // ── Status indicator colors ──────────────────────────────────────────────

    [Header("Status Indicator Colors")]
    [Tooltip("Color when a connection is established")]
    public Color connectedColor = new Color(0f, 1f, 0.4f, 1f);

    [Tooltip("Color when a connection is lost")]
    public Color disconnectedColor = new Color(1f, 0.3f, 0.3f, 1f);

    [Tooltip("Color while waiting / connecting")]
    public Color waitingColor = new Color(1f, 0.7f, 0f, 1f);

    // ── Buttons ──────────────────────────────────────────────────────────────

    [Header("Button Colors")]
    [Tooltip("Normal button background color")]
    public Color buttonNormalColor = new Color(0.2f, 0.6f, 1f, 1f);

    [Tooltip("Button background when hovered / highlighted")]
    public Color buttonHighlightedColor = new Color(0.3f, 0.7f, 1f, 1f);

    [Tooltip("Button background when pressed")]
    public Color buttonPressedColor = new Color(0.1f, 0.4f, 0.8f, 1f);

    [Tooltip("Button background when disabled")]
    public Color buttonDisabledColor = new Color(0.35f, 0.35f, 0.35f, 0.5f);

    [Tooltip("Button text color")]
    public Color buttonTextColor = Color.white;
}
