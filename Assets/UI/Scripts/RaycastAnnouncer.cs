using UnityEngine;
using System.Collections.Generic; // Required for List<>
using Meta.WitAi.TTS.Utilities;  // Required for TTSSpeaker
using System.Text.RegularExpressions;

// Automatically adds required components if they don't exist
[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(AudioSource))] // Ensure AudioSource exists
public class RaycastAnnouncer : MonoBehaviour
{
	[Header("TTS Settings")]
	[SerializeField]
	[Tooltip("Assign the TTSSpeaker component from your scene here.")]
	private TTSSpeaker ttsSpeaker;

	[Header("Audio Settings")]
	[SerializeField]
	[Tooltip("The sound effect to play upon detecting a new valid object.")]
	private AudioClip contactSound; // AudioClip field for the click sound

	[Header("SphereCast Settings")]
	[SerializeField]
	[Tooltip("The radius of the sphere used for detection (in meters). 0.1 = 10cm.")]
	private float detectionRadius = 0.1f;
	[SerializeField]
	[Tooltip("The maximum distance the spherecast will check.")]
	private float maxCastDistance = 100f;
	[SerializeField]
	[Tooltip("Specify layers the spherecast should interact with. Default is Everything.")]
	private LayerMask detectionLayerMask = ~0;

	[Header("Ignore Settings")]
	[SerializeField]
	[Tooltip("GameObjects in this list will not be announced if hit.")]
	private List<GameObject> ignoreList = new List<GameObject>();

	[Header("Cooldown Settings")]
	[SerializeField]
	[Tooltip("Minimum time (in seconds) that must pass between ANY automatic TTS announcement.")]
	private float announcementCooldown = 2.0f; // General cooldown between any announcements
	[SerializeField] // <<< NEW
	[Tooltip("Minimum time (in seconds) before the SAME object can be announced again. Should be >= Announcement Cooldown.")]
	private float repeatAnnounceDelay = 10.0f; // Specific delay for repeating the same object

	[Header("Visualization (Line Renderer)")]
	[SerializeField]
	[Tooltip("Color of the center line when hitting a valid, announceable object.")]
	private Color hitColor = Color.green;
	[SerializeField]
	[Tooltip("Color of the center line when hitting an object in the ignore list.")]
	private Color ignoredColor = Color.yellow;
	[SerializeField]
	[Tooltip("Color of the center line when not hitting any object.")]
	private Color noHitColor = Color.red;


	// --- Private State Variables ---
	private GameObject currentlyHitObject = null; // Object hit in the current frame
	private GameObject lastAnnouncedObject = null; // Tracks the last object announced via TTS
	private LineRenderer lineRenderer;
	private AudioSource audioSource; // Reference to AudioSource
	private float lastAnnouncementTime = -Mathf.Infinity; // Time for general cooldown
	private RaycastHit lastHitInfo; // Store hit info for Gizmo drawing
	private bool wasHitLastFrame = false; // Store hit status for Gizmo drawing
	public bool describe =false;
	string lastHit;
	string messageToSpeak;
	private static readonly Regex nameCleaningRegex = new Regex(@"(\s*\(\d+\)|\d+)$", RegexOptions.Compiled);
	/// <summary>
	/// Called when the script instance is being loaded.
	/// </summary>
	void Start()
	{
		// --- Initial Checks ---
		if (ttsSpeaker == null) { /* Error Log */ }
		lineRenderer = GetComponent<LineRenderer>();
		if (lineRenderer == null) { /* Error Log */ } else { /* Configure */ }
		audioSource = GetComponent<AudioSource>();
		if (audioSource == null) { /* Error Log */ } else { audioSource.playOnAwake = false; }

		// Ensure repeat delay is sensible
		if (repeatAnnounceDelay < announcementCooldown)
		{
			Debug.LogWarning($"Repeat Announce Delay ({repeatAnnounceDelay}s) is less than Announcement Cooldown ({announcementCooldown}s). Setting Repeat Delay equal to Cooldown.", this);
			repeatAnnounceDelay = announcementCooldown;
		}

		// Initialize state
		currentlyHitObject = null;
		lastAnnouncedObject = null;
		wasHitLastFrame = false;
		lastAnnouncementTime = -Mathf.Infinity; // Allow first announcement
	}
	
	
	/// <summary>
	/// Called every frame. Handles spherecasting, automatic TTS announcements (with general cooldown and same-object repeat delay),
	/// click sound logic, and visualization updates.
	/// </summary>
	void Update()
	{
		// Exit early if essential components are missing
		if (ttsSpeaker == null || lineRenderer == null || audioSource == null)
		{
			if (lineRenderer != null && lineRenderer.enabled) lineRenderer.enabled = false;
			wasHitLastFrame = false; // Reset state if components missing
			return;
		}
		if (!lineRenderer.enabled) lineRenderer.enabled = true;

		// --- SphereCast Preparation ---
		RaycastHit hitInfo;
		Vector3 origin = transform.position;
		Vector3 direction = transform.forward;

		// --- Perform SphereCast ---
		bool hitDetected = Physics.SphereCast(origin, detectionRadius, direction, out hitInfo, maxCastDistance, detectionLayerMask);
		wasHitLastFrame = hitDetected; // Store for Gizmo
		if (hitDetected) lastHitInfo = hitInfo; // Store for Gizmo


		// --- Process SphereCast Result ---
		Vector3 lineEndPoint;
		Color currentLineColor;
		GameObject objectJustHit = hitDetected ? hitInfo.collider.gameObject : null; // Store object hit *this frame*

		if (hitDetected)
		{
			// --- Sphere Hit Something ---
			currentlyHitObject = objectJustHit; // Update state
			lineEndPoint = origin + direction * hitInfo.distance;

			// Check if the hit object should be ignored
			bool isIgnored = ignoreList.Contains(currentlyHitObject);

			if (!isIgnored)
			{
				// --- Hit a Valid Object (Not Ignored) ---
				currentLineColor = hitColor;

				// Check Cooldowns and Object Identity
				bool generalCooldownElapsed = (Time.time >= lastAnnouncementTime + announcementCooldown);

				if (generalCooldownElapsed)
				{
					bool announce = false; // Flag to determine if we should announce

					if (currentlyHitObject != lastAnnouncedObject)
					{
						// --- Case 1: Different object detected ---
						announce = true; // Announce if general cooldown passed
					}
					else
					{
						// --- Case 2: Same object detected ---
						// Check the specific repeat delay for the *same* object
						if (Time.time >= lastAnnouncementTime + repeatAnnounceDelay)
						{
							announce = true; // Announce if repeat delay passed
						}
						// else: Same object, but too soon to repeat - do nothing
					}

					// --- Announce and Play Sound (if conditions met) ---
					if (announce)
					{
						// Play Sound Effect
						if (contactSound != null) { audioSource.PlayOneShot(contactSound); }
						else { /* Warning Log */ }
						
							// Announce via TTS
							string objectName = currentlyHitObject.name;
							float distance = hitInfo.distance;
							string distanceString = distance.ToString("F1");
						string clean = CleanName(objectName);
							messageToSpeak = $"{clean} is {distanceString} meters away";

							Debug.Log($"Announcing: \"{messageToSpeak}\" (Object: {(currentlyHitObject == lastAnnouncedObject ? "Same" : "New")})");
						

							// Update state AFTER announcing
							lastAnnouncedObject = currentlyHitObject; // Remember this object
							lastAnnouncementTime = Time.time;      // Reset timer for BOTH cooldowns		// Announce via TTS
						
					}
				}
				// If general cooldown hasn't elapsed, do nothing this frame
			}
			else
			{
				// --- Hit an Ignored Object ---
				currentLineColor = ignoredColor;
				// Don't clear lastAnnouncedObject here - let the memory persist
				currentlyHitObject = null; // Still treat current hit as null if ignored
			}
		}
		else
		{
			// --- Sphere Hit Nothing ---
			currentlyHitObject = null; // Clear currently hit object
			lineEndPoint = origin + direction * maxCastDistance;
			currentLineColor = noHitColor;
			// Don't clear lastAnnouncedObject here - let the memory persist
		}
		if (describe)
		{
			Debug.LogWarning("ray describe logic triggered");
			ttsSpeaker.SpeakQueued(messageToSpeak);
			describe = false;
		}

		// --- Update Line Renderer Visualization (Center Line) ---
		lineRenderer.SetPosition(0, origin);
		lineRenderer.SetPosition(1, lineEndPoint);
		lineRenderer.startColor = currentLineColor;
		lineRenderer.endColor = currentLineColor;
	}


	/// <summary>
	/// Draw Gizmos in the Scene view for editor visualization.
	/// </summary>
	private void OnDrawGizmos()
	{
		if (!enabled) return; // Don't draw if component is disabled

		Vector3 origin = transform.position;
		Vector3 direction = transform.forward;
		Color gizmoColor;
		float distance;
		bool drawHitSphere = false;

		// Determine color and distance based on last frame's result
		// Note: Gizmo logic might slightly lag behind Update logic visually
		if (Application.isPlaying && wasHitLastFrame)
		{
			bool isIgnored = false;
			GameObject hitGO = null; // GameObject hit in the last frame
			if (lastHitInfo.collider != null)
			{
				hitGO = lastHitInfo.collider.gameObject;
				isIgnored = ignoreList.Contains(hitGO);
			}
			gizmoColor = isIgnored ? ignoredColor : hitColor;
			distance = lastHitInfo.distance;
			drawHitSphere = true;
		}
		else if (Application.isPlaying && !wasHitLastFrame)
		{
			gizmoColor = noHitColor;
			distance = maxCastDistance;
			drawHitSphere = false;
		}
		else // Not playing, just show potential cast
		{
			gizmoColor = Color.gray;
			distance = maxCastDistance;
			drawHitSphere = true; // Show potential end sphere
		}

		Gizmos.color = gizmoColor;

		// Draw the wire sphere at the start position
		Gizmos.DrawWireSphere(origin, detectionRadius);

		// Draw the wire sphere at the end position (hit point or max distance)
		distance = Mathf.Max(0, distance);
		Vector3 endPosition = origin + direction * distance;

		if (drawHitSphere) // Only draw end sphere if hitting something or not playing
		{
			Gizmos.DrawWireSphere(endPosition, detectionRadius);
		}

		// Draw the center line connecting the sphere centers
		Gizmos.DrawLine(origin, endPosition);
	}
	private string CleanName(string originalName)
	{
		if (string.IsNullOrEmpty(originalName)) return "object";
		string cleaned = nameCleaningRegex.Replace(originalName, "").Trim();
		return string.IsNullOrEmpty(cleaned) ? "object" : cleaned;
	}
}
