using UnityEngine;
using Meta.WitAi.TTS.Utilities; // Required for TTSSpeaker

// Ensure necessary components exist on the same GameObject
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))] // Rigidbody is required for Trigger events
public class CollisionAnnouncerRelative : MonoBehaviour
{
	[Header("TTS Configuration")]
	[Tooltip("Assign the TTSSpeaker component from your scene here.")]
	public TTSSpeaker ttsSpeaker; // Assign this in the Unity Inspector

	[Header("Relative Position Settings")]
	[Tooltip("Threshold for dot product to determine primary direction (higher value = stricter alignment)")]
	[Range(0f, 1f)]
	public float directionThreshold = 0.3f; // Objects need to be somewhat aligned with an axis

	private Collider ownCollider;

	void Awake()
	{
		ownCollider = GetComponent<Collider>();

		// Check if the collider is set to trigger
		if (!ownCollider.isTrigger)
		{
			Debug.LogWarning($"CollisionAnnouncerRelative on {gameObject.name}: Collider is not set to 'Is Trigger'. Trigger events (OnTriggerEnter) will not fire. Please enable 'Is Trigger'.", this);
			// Optionally force it:
			// ownCollider.isTrigger = true;
			// Debug.LogWarning($"CollisionAnnouncerRelative on {gameObject.name}: Set Collider to 'Is Trigger'.", this);
		}

		// Check for Rigidbody (already required by attribute, but good practice)
		if (GetComponent<Rigidbody>() == null)
		{
			Debug.LogError($"CollisionAnnouncerRelative on {gameObject.name}: Rigidbody component is missing. It is required for trigger events.", this);
		}

		// Find TTSSpeaker dynamically if not assigned (assignment is preferred)
		if (ttsSpeaker == null)
		{
			ttsSpeaker = FindObjectOfType<TTSSpeaker>();
			if (ttsSpeaker == null)
			{
				Debug.LogWarning($"CollisionAnnouncerRelative on {gameObject.name}: TTSSpeaker is not assigned and couldn't be found dynamically. Announcements will not work.", this);
			}
			else
			{
				Debug.LogWarning($"CollisionAnnouncerRelative on {gameObject.name}: TTSSpeaker was found dynamically. Assigning it directly in the Inspector is recommended.", this);
			}
		}
	}

	/// <summary>
	/// Unity Trigger Event: Called when another Collider enters this trigger.
	/// </summary>
	/// <param name="otherCollider">The Collider of the object that entered the trigger.</param>
	void OnTriggerEnter(Collider otherCollider)
	{
		if (ttsSpeaker == null)
		{
			Debug.LogError("Cannot announce: TTSSpeaker reference is missing.", this);
			return;
		}

		// Make sure we don't trigger on ourselves if nested colliders exist
		if (otherCollider == ownCollider || otherCollider.gameObject == this.gameObject)
		{
			return;
		}

		GameObject targetObject = otherCollider.gameObject;

		// --- Calculate Relative Position ---
		string relativePosition = GetRelativePosition(otherCollider.transform.position); // Use the position of the incoming collider's transform

		// --- Construct and Queue Announcement ---
		// Example Format: "Collided with Cube from above."
		string announcement = $"Collided with {targetObject.name} from {relativePosition}.";
		Debug.Log($"CollisionAnnouncerRelative: Queuing announcement: {announcement}"); // For debugging
		ttsSpeaker.SpeakQueued(announcement);
	}

	/// <summary>
	/// Calculates the relative position (e.g., "in front", "behind", "above", "below")
	/// of a target position compared to this object's position and orientation.
	/// </summary>
	/// <param name="targetPosition">The world position of the target object.</param>
	/// <returns>A string describing the relative position.</returns>
	private string GetRelativePosition(Vector3 targetPosition)
	{
		// Vector from this object to the target object
		Vector3 directionToTarget = (targetPosition - transform.position);

		// Project the direction vector onto the object's local axes using normalized direction
		float forwardAmount = Vector3.Dot(transform.forward, directionToTarget.normalized);
		float upAmount = Vector3.Dot(transform.up, directionToTarget.normalized);
		// Optional: float rightAmount = Vector3.Dot(transform.right, directionToTarget.normalized);

		// Determine the dominant direction based on the dot products
		float absForward = Mathf.Abs(forwardAmount);
		float absUp = Mathf.Abs(upAmount);
		// Optional: float absRight = Mathf.Abs(rightAmount);

		// Default position if no strong direction is found or below threshold
		string position = "nearby"; // Or perhaps "the side"? "nearby" is safer.

		// Check if alignment exceeds the threshold for primary axes
		if (absForward > directionThreshold || absUp > directionThreshold) // Add absRight here if using left/right
		{
			if (absForward >= absUp) // Primarily forward or backward (use >= to prioritize front/back slightly if equal)
			{
				position = (forwardAmount > 0) ? "in front" : "behind";
			}
			else // Primarily above or below
			{
				position = (upAmount > 0) ? "above" : "below";
			}
			// Optional: Add Left/Right check here if needed
			// else if (absRight > directionThreshold) {
			//    position = (rightAmount > 0) ? "the right" : "the left";
			// }
		}

		return position;
	}

	// Optional Gizmo to show forward/up vectors for orientation reference
	void OnDrawGizmosSelected()
	{
		// Draw forward vector
		Gizmos.color = Color.blue;
		Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
		// Draw up vector
		Gizmos.color = Color.green;
		Gizmos.DrawLine(transform.position, transform.position + transform.up * 1.5f);
	}
}