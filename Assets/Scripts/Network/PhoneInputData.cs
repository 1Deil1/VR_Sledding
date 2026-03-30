using System;

/// <summary>
/// Data transfer object representing one tilt sample from the player's phone.
/// Matches the JSON structure sent by phone_controller.html via WebSocket.
///
/// pitch  — device beta  (forward / backward tilt, degrees)
/// roll   — device gamma (left / right tilt, degrees)
/// </summary>
[Serializable]
public class PhoneInputData
{
    public float pitch;   // Positive = nose of phone pointing down (lean forward)
    public float roll;    // Positive = right side of phone pointing down (lean right)

    public PhoneInputData() { }

    public PhoneInputData(float pitch, float roll)
    {
        this.pitch = pitch;
        this.roll  = roll;
    }

    public override string ToString() => $"Pitch: {pitch:F1}°  Roll: {roll:F1}°";
}
