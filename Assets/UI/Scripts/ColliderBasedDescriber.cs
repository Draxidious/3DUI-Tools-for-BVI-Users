using UnityEngine;
using Meta.WitAi.TTS.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Text; // Required for StringBuilder
using System.Text.RegularExpressions;
using OpenCover.Framework.Model;
using UnityEngine.tvOS;
using UnityEngine.UIElements;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class ColliderBasedDescriber : MonoBehaviour
{
	[Header("TTS Configuration")]
	[Tooltip("Assign the TTSSpeaker component from your scene here.")]
	public TTSSpeaker ttsSpeaker;

	[Header("Scene Query Settings")]
	[Tooltip("Layers to include in the object search.")]
	public LayerMask checkLayers = Physics.DefaultRaycastLayers;
	[Tooltip("List of specific GameObjects to ignore.")]
	public List<GameObject> objectsToIgnore = new List<GameObject>();

	[Header("Relative Position Settings")]
	[Tooltip("Threshold for dot product to determine primary direction.")]
	[Range(0f, 1f)]
	public float directionThreshold = 0.3f;
	[Tooltip("Max vertical distance between object bounds bottom/top to be considered 'on top of'.")]
	public float onTopVerticalTolerance = 0.1f;

	[Header("Description Settings")]
	[Tooltip("Minimum time (seconds) between descriptions triggered by changes.")]
	public float descriptionCooldown = 3.0f;
	[Tooltip("Short delay (seconds) after a change before triggering description.")]
	public float descriptionUpdateDelay = 0.2f;

	private Collider ownCollider;
	private float _timeOfLastDescription = -100f;
	private HashSet<Collider> _objectsInTrigger = new HashSet<Collider>();
	private bool _needsDescriptionUpdate = false;
	private float _timeSinceLastChange = 0f;
	private static readonly Regex nameCleaningRegex = new Regex(@"(\s*\(\d+\)|\d+)$", RegexOptions.Compiled);

	void Awake()
	{
		ownCollider = GetComponent<Collider>();
		if (ownCollider == null) { Debug.LogError($"ColliderBasedDescriber on {gameObject.name}: Cannot find Collider component!", this); return; }
		if (!ownCollider.isTrigger) { Debug.LogWarning($"ColliderBasedDescriber on {gameObject.name}: Collider not set to 'Is Trigger'. Forcing true.", this); ownCollider.isTrigger = true; }
		if (GetComponent<Rigidbody>() == null) { Debug.LogError($"ColliderBasedDescriber on {gameObject.name}: Rigidbody component missing.", this); }

		if (ttsSpeaker == null)
		{
			ttsSpeaker = FindObjectOfType<TTSSpeaker>();
			if (ttsSpeaker == null) { Debug.LogWarning($"ColliderBasedDescriber on {gameObject.name}: TTSSpeaker not assigned/found.", this); }
			else { Debug.LogWarning($"ColliderBasedDescriber on {gameObject.name}: TTSSpeaker found dynamically.", this); }
		}
		_timeOfLastDescription = -descriptionCooldown;
	}

	void OnEnable() { /* No changes needed */ }
	void OnDisable() { /* No changes needed */ }

	private void OnTriggerEnter(Collider other)
	{
		if (other == ownCollider || other.gameObject == this.gameObject || IsInExcludedHierarchy(other.gameObject)) return;
		if (_objectsInTrigger.Add(other))
		{
			Debug.Log($"ColliderBasedDescriber: Object Entered: {other.gameObject.name}. Flagging for update.");
			FlagForDescriptionUpdate();
		}
	}

	private void OnTriggerExit(Collider other)
	{
		if (other == ownCollider || other.gameObject == this.gameObject || IsInExcludedHierarchy(other.gameObject)) return;
		if (_objectsInTrigger.Remove(other))
		{
			Debug.Log($"ColliderBasedDescriber: Object Exited: {other.gameObject.name}. Flagging for update.");
			FlagForDescriptionUpdate();
		}
	}

	private void FlagForDescriptionUpdate()
	{
		_needsDescriptionUpdate = true;
		_timeSinceLastChange = 0f;
	}

	void Update()
	{
		if (_needsDescriptionUpdate)
		{
			_timeSinceLastChange += Time.deltaTime;
			if (_timeSinceLastChange >= descriptionUpdateDelay)
			{
				if (Time.time >= _timeOfLastDescription + descriptionCooldown)
				{
					Debug.Log("ColliderBasedDescriber: Update delay and cooldown passed. Describing objects.");
					DescribeOverlappingObjects();
					_timeOfLastDescription = Time.time;
				}
				else
				{
					Debug.Log("ColliderBasedDescriber: Update delay passed, but main cooldown active.");
				}
				_needsDescriptionUpdate = false;
			}
		}
	}

	// ---- Description Logic ----
	public void DescribeOverlappingObjects()
	{
		if (ttsSpeaker == null) { Debug.LogError("TTS Speaker missing!", this); return; }
		if (ownCollider == null) { Debug.LogError("Own Collider missing!", this); return; }

		Debug.Log("ColliderBasedDescriber: Running DescribeOverlappingObjects...");

		Collider[] overlappingColliders = Physics.OverlapBox(ownCollider.bounds.center, ownCollider.bounds.extents, transform.rotation, checkLayers);
		List<GameObject> validObjects = new List<GameObject>();
		// Use a temporary hash set to ensure we only consider colliders currently in the trigger state
		HashSet<Collider> currentTriggerColliders = new HashSet<Collider>(_objectsInTrigger);

		foreach (Collider col in overlappingColliders)
		{
			GameObject obj = col.gameObject;
			// Filter self, ignored objects, and objects no longer physically in the trigger (according to trigger state)
			if (obj == this.gameObject || col == ownCollider || IsInExcludedHierarchy(obj) || !currentTriggerColliders.Contains(col)) continue;
			if (!validObjects.Contains(obj)) { validObjects.Add(obj); }
		}

		if (validObjects.Count == 0)
		{
			ttsSpeaker.SpeakQueued("Nothing significant is here.");
			Debug.Log("ColliderBasedDescriber: DescribeOverlappingObjects found no valid objects currently in trigger.");
			return;
		}

		GameObject primaryObject = validObjects.OrderBy(obj => Vector3.Distance(transform.position, obj.transform.position)).FirstOrDefault();
		if (primaryObject == null) { return; }

		string primaryName = CleanName(primaryObject.name);

		// --- Build description clauses directly ---
		StringBuilder descriptionBuilder = new StringBuilder();
		descriptionBuilder.Append($"There is a {primaryName} here");
		int clauseCount = 0;

		foreach (GameObject secondaryObject in validObjects)
		{
			if (secondaryObject == primaryObject) continue;

			string position = GetRelativePosition(secondaryObject.transform, primaryObject.transform);
			// Skip adding clauses for objects that are just "near" unless it's the only relationship? Decide based on preference.
			// Let's include "near" for now. If you want to exclude it, add: if (position == "near") continue;

			string secondaryName = CleanName(secondaryObject.name);
			string objectListString = FormatObjectList(new List<string> { secondaryName }); // Format just the single object name

			string conjunction = clauseCount == 0 ? " with " : " and "; // Use "with" for first, "and" for subsequent
			descriptionBuilder.Append($"{conjunction}{objectListString} {position} it");
			clauseCount++;
		}
		// -----------------------------------------

		descriptionBuilder.Append("."); // End with a period
		string finalDescription = descriptionBuilder.ToString();

		ttsSpeaker.SpeakQueued(finalDescription);
		Debug.Log($"ColliderBasedDescriber: Queued TTS: {finalDescription}");
	}

	// ---- Helper Functions ----

	private string CleanName(string originalName)
	{
		if (string.IsNullOrEmpty(originalName)) return "object";
		string cleaned = nameCleaningRegex.Replace(originalName, "").Trim();
		return string.IsNullOrEmpty(cleaned) ? "object" : cleaned;
	}

	// Simplified FormatObjectList as we now describe one object per clause
	private string FormatObjectList(List<string> names)
	{
		if (names == null || names.Count == 0) return "";
		// Simple "a " prefix, could be enhanced for "an "
		return $"a {names[0]}";
	}


	private bool IsInExcludedHierarchy(GameObject obj)
	{
		Transform currentTransform = obj.transform;
		while (currentTransform != null) { if (objectsToIgnore.Contains(currentTransform.gameObject)) { return true; } currentTransform = currentTransform.parent; }
		return false;
	}

	/// <summary>
	/// Calculates relative position. Checks for "on top of" and combines horizontal directions.
	/// </summary>
	private string GetRelativePosition(Transform targetTransform, Transform referenceTransform)
	{
		Vector3 directionToReference = (targetTransform.position - referenceTransform.position);
		if (directionToReference.sqrMagnitude < 0.0001f) return "at the same position as";

		Vector3 normalizedDirection = directionToReference.normalized;

		// Calculate all dot products
		float forwardAmount = Vector3.Dot(referenceTransform.forward, normalizedDirection);
		float rightAmount = Vector3.Dot(referenceTransform.right, normalizedDirection);
		float worldUpAmount = Vector3.Dot(Vector3.up, normalizedDirection); // World Up/Down

		float absForward = Mathf.Abs(forwardAmount);
		float absUp = Mathf.Abs(worldUpAmount);
		float absRight = Mathf.Abs(rightAmount);
		float threshold = directionThreshold;

		// --- Prioritize Vertical ---
		// Check if vertical is the clearly dominant axis OR if it's specifically "on top"
		if (absUp >= absForward && absUp >= absRight && absUp > threshold)
		{
			if (worldUpAmount > 0) // Above or On Top
			{
				return IsDirectlyOnTop(targetTransform, referenceTransform) ? "on top of" : "above";
			}
			else // Below
			{
				return "below";
			}
		}
		// Special check for "on top" even if vertical isn't strictly dominant but conditions are met
		// This helps catch cases where an object is slightly off-center but clearly on top.
		// We check if worldUpAmount is positive (generally above) and the IsDirectlyOnTop check passes.
		if (worldUpAmount > (threshold * 0.5f) && IsDirectlyOnTop(targetTransform, referenceTransform)) // Requires some upward direction
		{
			return "on top of";
		}

		// --- Handle Horizontal Plane (if vertical wasn't dominant) ---
		bool forwardSignificant = absForward > threshold;
		bool rightSignificant = absRight > threshold;

		if (forwardSignificant && rightSignificant)
		{
			// Combine directions
			string fb = (forwardAmount > 0) ? "in front" : "behind";
			string lr = (rightAmount > 0) ? "to the right" : "to the left";
			return $"{fb} and {lr} of"; // e.g., "in front and to the right of"
		}
		else if (forwardSignificant)
		{
			// Only forward/backward is significant
			return (forwardAmount > 0) ? "in front of" : "behind";
		}
		else if (rightSignificant)
		{
			// Only right/left is significant
			return (rightAmount > 0) ? "to the right of" : "to the left of";
		}
		else
		{
			// No direction met the threshold significantly
			return "near";
		}
	}

	private bool IsDirectlyOnTop(Transform topTransform, Transform bottomTransform)
	{
		Collider topCollider = topTransform.GetComponent<Collider>();
		Collider bottomCollider = bottomTransform.GetComponent<Collider>();
		if (topCollider == null || bottomCollider == null) return false;

		Bounds topBounds = topCollider.bounds;
		Bounds bottomBounds = bottomCollider.bounds;

		bool overlapsHorizontally =
			topBounds.min.x < bottomBounds.max.x && topBounds.max.x > bottomBounds.min.x &&
			topBounds.min.z < bottomBounds.max.z && topBounds.max.z > bottomBounds.min.z;

		if (!overlapsHorizontally) return false;

		float verticalGap = topBounds.min.y - bottomBounds.max.y;
		return verticalGap >= -onTopVerticalTolerance && verticalGap <= onTopVerticalTolerance;
	}

	// Optional Gizmo
	void OnDrawGizmosSelected()
	{
		if (ownCollider == null) { ownCollider = GetComponent<Collider>(); if (ownCollider == null) return; }
		Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
		Gizmos.matrix = Matrix4x4.TRS(ownCollider.bounds.center, transform.rotation, Vector3.one);
		Gizmos.DrawCube(Vector3.zero, ownCollider.bounds.size);
		Gizmos.matrix = Matrix4x4.identity;
	}
}
