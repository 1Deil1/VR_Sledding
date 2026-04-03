using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Static helper used by the UI scripts to apply UIStyleConfig to panels,
/// buttons, and text at runtime.
/// </summary>
public static class UIStyleApplier
{
    /// <summary>
    /// Sets the background color on the panel's own Image component.
    /// Only targets the root GameObject — never searches children.
    /// </summary>
    public static void ApplyPanelColor(GameObject panel, Color color)
    {
        if (panel == null) return;
        var img = panel.GetComponent<Image>();
        if (img != null)
            img.color = color;
    }

    /// <summary>
    /// Sets the color on a single TextMeshProUGUI element.
    /// </summary>
    public static void ApplyTextColor(TextMeshProUGUI text, Color color)
    {
        if (text == null) return;
        text.enableVertexGradient = false;
        text.color = color;
    }

    /// <summary>
    /// Applies text color to ALL TextMeshProUGUI children of a panel
    /// and ensures each one renders above the panel background by giving
    /// it a sub-Canvas with a higher sorting order.
    /// This fixes World Space Canvas text being hidden behind Image quads.
    /// </summary>
    public static void ApplyTextColorToAll(GameObject panel, Color color)
    {
        if (panel == null) return;
        foreach (var tmp in panel.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            ApplyTextColor(tmp, color);
            EnsureRendersAbovePanel(tmp.gameObject);
        }
    }

    /// <summary>
    /// Adds a sub-Canvas with overrideSorting to a UI element so it renders
    /// above sibling/parent Image components in a World Space Canvas.
    /// Only adds once — subsequent calls are no-ops.
    /// </summary>
    private static void EnsureRendersAbovePanel(GameObject go)
    {
        var subCanvas = go.GetComponent<Canvas>();
        if (subCanvas == null)
        {
            subCanvas = go.AddComponent<Canvas>();
            subCanvas.overrideSorting = true;
            subCanvas.sortingOrder = 1;
        }
    }

    /// <summary>
    /// Configures all four color states of a Unity UI Button.
    /// </summary>
    public static void ApplyButtonColors(Button button, UIStyleConfig cfg)
    {
        if (button == null || cfg == null) return;

        var cb = button.colors;
        cb.normalColor      = cfg.buttonNormalColor;
        cb.highlightedColor = cfg.buttonHighlightedColor;
        cb.pressedColor     = cfg.buttonPressedColor;
        cb.disabledColor    = cfg.buttonDisabledColor;
        button.colors = cb;

        // Also colour the button label if it has one
        var label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.color = cfg.buttonTextColor;
    }
}
