using UnityEngine;

/// <summary>
/// Attached to each tree and rock obstacle.
/// Notifies GameManager when the sled collides with this object.
///
/// Setup: the Collider on this GameObject must have "Is Trigger" enabled.
/// The Sled GameObject must be tagged "Sled".
/// </summary>
[RequireComponent(typeof(Collider))]
public class Obstacle : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Sled"))
        {
            GameManager.Instance?.OnSledHitObstacle();
        }
    }
}
