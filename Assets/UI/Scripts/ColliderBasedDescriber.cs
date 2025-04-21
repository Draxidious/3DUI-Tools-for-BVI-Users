using UnityEngine;
using Meta.WitAi.TTS.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Text; // Required for StringBuilder
using System.Text.RegularExpressions;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class ColliderBasedDescriber : MonoBehaviour
{
	[Header("TTS Configuration")]
	[Tooltip("Assign the TTSSpeaker component from your scene here.")]
	public TTSSpeaker ttsSpeaker;
	[Tooltip("Maximum character length for each TTS chunk.")]
	public int ttsChunkMaxLength = 250; // Keep below TTS service limit (e.g., 280)

	[Header("Scene Query Settings")]
	[Tooltip("Layers to include in the object search.")]
	public LayerMask checkLayers = Physics.DefaultRaycastLayers;
	[Tooltip("List of specific GameObjects (and their children) to ignore.")]
	public List<GameObject> objectsToIgnore = new List<GameObject>();

	[Header("Relative Position Settings")]
	[Tooltip("Threshold for dot product to determine primary direction.")]
	[Range(0f, 1f)]
	public float directionThreshold = 0.3f;
	[Tooltip("Max vertical distance between object bounds bottom/top to be considered 'on top of'.")]
	public float onTopVerticalTolerance = 0.1f;

	[Header("Description Settings")]
	[Tooltip("Set this to true from another script to speak the latest description.")]
	public bool describe = false; // Flag re-added for manual triggering
	[Tooltip("Minimum time (seconds) between generating updated description text.")]
	public float descriptionCooldown = 3.0f;
	[Tooltip("Short delay (seconds) after a change before generating updated description text.")]
	public float descriptionUpdateDelay = 0.2f;

	// --- Private Variables ---
	private Collider ownCollider;
	private float _timeOfLastDescription = -100f;
	private HashSet<Collider> _objectsInTrigger = new HashSet<Collider>();
	private bool _needsDescriptionUpdate = false;
	private float _timeSinceLastChange = 0f;
	private static readonly Regex nameCleaningRegex = new Regex(@"(\s*\(\d+\)|\d+)$", RegexOptions.Compiled);
	private string _lastGeneratedFullDescription = ""; // Store the latest generated description


	void Awake()
	{
		ownCollider = GetComponent<Collider>();
		if (ownCollider == null)
		{
			Debug.LogError($"ColliderBasedDescriber on {gameObject.name}: Cannot find Collider component!", this);
			enabled = false;
			return;
		}
		if (!ownCollider.isTrigger)
		{
			Debug.LogWarning($"ColliderBasedDescriber on {gameObject.name}: Collider not set to 'Is Trigger'. Forcing true.", this);
			ownCollider.isTrigger = true;
		}

		Rigidbody rb = GetComponent<Rigidbody>();
		if (rb == null)
		{
			Debug.LogError($"ColliderBasedDescriber on {gameObject.name}: Rigidbody component missing.", this);
			enabled = false;
			return;
		}

		if (ttsSpeaker == null)
		{
			ttsSpeaker = FindObjectOfType<TTSSpeaker>();
			if (ttsSpeaker == null) { Debug.LogWarning($"ColliderBasedDescriber on {gameObject.name}: TTSSpeaker not assigned/found. Manual descriptions won't work.", this); }
			else { Debug.Log($"ColliderBasedDescriber on {gameObject.name}: TTSSpeaker found dynamically.", this); }
		}
		_timeOfLastDescription = -descriptionCooldown; // Allow immediate generation on first trigger
	}


	private void OnTriggerEnter(Collider other)
	{
		if (other == ownCollider || other.gameObject == this.gameObject || IsInExcludedHierarchy(other.gameObject) || !IsInCheckLayers(other.gameObject.layer)) return;
		if (_objectsInTrigger.Add(other))
		{
			FlagForDescriptionUpdate();
		}
	}

	private void OnTriggerExit(Collider other)
	{
		if (other == ownCollider || other.gameObject == this.gameObject || IsInExcludedHierarchy(other.gameObject)) return;
		if (_objectsInTrigger.Remove(other))
		{
			FlagForDescriptionUpdate();
		}
	}

	private void FlagForDescriptionUpdate()
	{
		_needsDescriptionUpdate = true;
		_timeSinceLastChange = 0f; // Reset delay timer on new change
	}

	void Update()
	{
		// Part 1: Update the internal description text based on triggers and cooldowns
		if (_needsDescriptionUpdate)
		{
			_timeSinceLastChange += Time.deltaTime;
			// Check if delay has passed AND cooldown has passed
			if (_timeSinceLastChange >= descriptionUpdateDelay && Time.time >= _timeOfLastDescription + descriptionCooldown)
			{
				// Only generate the description text and store it internally
				_lastGeneratedFullDescription = GenerateFullDescription();
				// Debug.Log($"ColliderBasedDescriber: Description text updated internally: '{_lastGeneratedFullDescription}'");

				_timeOfLastDescription = Time.time; // Reset cooldown timer *after* generating
				_needsDescriptionUpdate = false; // Reset flag
				_timeSinceLastChange = 0f; // Reset delay timer
			}
		}

		// Part 2: Speak the last generated description if the 'describe' flag is set externally
		if (describe)
		{
			// Reset the flag *immediately* to prevent re-triggering multiple times
			describe = false;

			if (ttsSpeaker != null && !string.IsNullOrEmpty(_lastGeneratedFullDescription))
			{
				// Split the stored description into chunks
				List<string> descriptionChunks = SplitDescriptionIntoChunks(_lastGeneratedFullDescription, ttsChunkMaxLength);

				if (descriptionChunks.Count > 0)
				{
					// Debug.Log($"ColliderBasedDescriber: Speaking description in {descriptionChunks.Count} chunk(s) due to 'describe' flag.");
					foreach (string chunk in descriptionChunks)
					{
						if (!string.IsNullOrWhiteSpace(chunk))
						{
							ttsSpeaker.SpeakQueued(chunk);
							// Debug.Log($"--- Queued Chunk: '{chunk}' (Length: {chunk.Length})");
						}
					}
				}
				else
				{
					// This might happen if the description is just whitespace after trimming
					Debug.LogWarning("ColliderBasedDescriber: 'describe' flag was true, but splitting the description resulted in zero valid chunks.", this);
				}
			}
			else if (ttsSpeaker == null)
			{
				Debug.LogWarning("ColliderBasedDescriber: 'describe' flag set but TTSSpeaker is missing.", this);
			}
			else // Description is null/empty
			{
				// Optionally speak "Nothing to describe" or just do nothing silently.
				// Debug.Log("ColliderBasedDescriber: 'describe' flag set but no description text is available to speak.");
				// ttsSpeaker.SpeakQueued("There is nothing to describe right now.");
			}
		}
	}


	// ---- Description Generation ----

	/// <summary>
	/// Generates the complete descriptive string for all valid nearby objects
	/// and returns it. Does NOT speak.
	/// </summary>
	/// <returns>The full description string, or an empty string if no objects.</returns>
	private string GenerateFullDescription()
	{
		if (ownCollider == null) return "";

		_objectsInTrigger.RemoveWhere(col => col == null || !col.enabled || !col.gameObject.activeInHierarchy);

		List<Collider> currentNearbyColliders = _objectsInTrigger
			.Where(col => IsInCheckLayers(col.gameObject.layer) && !IsInExcludedHierarchy(col.gameObject))
			.ToList();

		if (currentNearbyColliders.Count == 0)
		{
			return ""; // Return empty string if nothing relevant nearby
		}

		Collider primaryCollider = currentNearbyColliders
			.OrderBy(col => Vector3.Distance(ownCollider.bounds.center, col.bounds.center))
			.FirstOrDefault();

		if (primaryCollider == null) return "";

		GameObject primaryObject = primaryCollider.gameObject;
		string primaryName = CleanName(primaryObject.name);

		List<Collider> secondaryColliders = currentNearbyColliders
			.Where(col => col != primaryCollider)
			.OrderBy(col => Vector3.Distance(ownCollider.bounds.center, col.bounds.center))
			.ToList();

		StringBuilder descriptionBuilder = new StringBuilder();
		descriptionBuilder.Append($"There is a {primaryName} here");

		int describedCount = 0;
		foreach (Collider secondaryCollider in secondaryColliders)
		{
			GameObject secondaryObject = secondaryCollider.gameObject;
			string position = GetRelativePosition(secondaryObject.transform, primaryObject.transform);
			string secondaryName = CleanName(secondaryObject.name);
			string objectListString = FormatObjectList(new List<string> { secondaryName });

			string conjunction = describedCount == 0 ? " with " : " and ";
			descriptionBuilder.Append($"{conjunction}{objectListString} {position} it");
			describedCount++;
		}

		descriptionBuilder.Append("."); // End with a period

		return descriptionBuilder.ToString();
	}


	/// <summary>
	/// Splits a long string into smaller chunks based on maxLength.
	/// Prioritizes splitting at spaces to avoid breaking words.
	/// If a single word is longer than maxLength, it will be split, and a warning logged.
	/// </summary>
	private List<string> SplitDescriptionIntoChunks(string text, int maxLength)
	{
		// (Implementation remains the same as the previous version - collider_describer_split_v1)
		List<string> chunks = new List<string>();
		if (string.IsNullOrEmpty(text) || maxLength <= 0)
		{
			// Debug.LogError("SplitDescriptionIntoChunks: Input text is null/empty or maxLength is invalid.", this);
			return chunks;
		}

		int currentIndex = 0;
		while (currentIndex < text.Length)
		{
			int remainingLength = text.Length - currentIndex;
			if (remainingLength <= maxLength)
			{
				chunks.Add(text.Substring(currentIndex).Trim());
				break;
			}

			int potentialEndIndex = currentIndex + maxLength;
			int splitIndex = -1;
			try
			{
				int searchStartIndex = potentialEndIndex - 1;
				int searchLength = maxLength;
				if (searchStartIndex < currentIndex) { searchStartIndex = currentIndex; searchLength = 0; }
				else if (searchStartIndex - currentIndex + 1 < searchLength) { searchLength = searchStartIndex - currentIndex + 1; }

				if (searchLength > 0) { splitIndex = text.LastIndexOf(' ', searchStartIndex, searchLength); }
				else { splitIndex = -1; }
			}
			catch (System.ArgumentOutOfRangeException ex)
			{
				Debug.LogError($"SplitDescriptionIntoChunks: Error finding space. currentIndex={currentIndex}, maxLength={maxLength}, textLength={text.Length}. Exception: {ex.Message}", this);
				splitIndex = -1;
			}

			int lengthToTake;
			if (splitIndex > currentIndex) { lengthToTake = splitIndex - currentIndex; }
			else
			{
				int nextSpace = text.IndexOf(' ', currentIndex);
				if (nextSpace == -1 || nextSpace >= potentialEndIndex)
				{
					lengthToTake = maxLength;
					if (nextSpace == -1 || nextSpace > potentialEndIndex)
					{
						// Debug.LogWarning($"ColliderBasedDescriber: Forced to split a long word/block near index {currentIndex}.", this);
					}
				}
				else { lengthToTake = maxLength; /* Fallback */ }
			}
			if (lengthToTake <= 0) { Debug.LogError($"SplitDescriptionIntoChunks: Zero/Negative length. Breaking. Index:{currentIndex}", this); break; }

			string chunk = text.Substring(currentIndex, lengthToTake).Trim();
			if (!string.IsNullOrEmpty(chunk)) { chunks.Add(chunk); }

			currentIndex += lengthToTake;
			if (currentIndex < text.Length && text[currentIndex] == ' ') { currentIndex++; }
		}
		return chunks;
	}


	// ---- Helper Functions ----
	// (CleanName, FormatObjectList, IsInCheckLayers, IsInExcludedHierarchy,
	//  GetRelativePosition, IsDirectlyOnTop - remain the same)

	private string CleanName(string originalName)
	{
		if (string.IsNullOrEmpty(originalName)) return "object";
		string cleaned = nameCleaningRegex.Replace(originalName, "").Trim();
		return string.IsNullOrEmpty(cleaned) ? "object" : cleaned;
	}
	private string FormatObjectList(List<string> names)
	{
		if (names == null || names.Count == 0) return "";
		string name = names[0];
		char firstChar = char.ToLower(name[0]);
		string article = ("aeiou".IndexOf(firstChar) >= 0) ? "an " : "a ";
		return article + name;
	}
	private bool IsInCheckLayers(int objectLayer) { return (checkLayers.value & (1 << objectLayer)) != 0; }
	private bool IsInExcludedHierarchy(GameObject obj)
	{
		if (objectsToIgnore.Count == 0) return false;
		Transform currentTransform = obj.transform;
		while (currentTransform != null)
		{
			if (objectsToIgnore.Contains(currentTransform.gameObject)) { return true; }
			currentTransform = currentTransform.parent;
		}
		return false;
	}
	private string GetRelativePosition(Transform targetTransform, Transform referenceTransform)
	{
		Vector3 directionToReference = (targetTransform.position - referenceTransform.position);
		if (directionToReference.sqrMagnitude < 0.0001f) return "at the same position as";
		Vector3 localDirection = referenceTransform.InverseTransformDirection(directionToReference.normalized);
		float forwardAmount = localDirection.z; float rightAmount = localDirection.x; float upAmount = localDirection.y;
		float absForward = Mathf.Abs(forwardAmount); float absUp = Mathf.Abs(upAmount); float absRight = Mathf.Abs(rightAmount);
		float threshold = directionThreshold;
		Vector3 worldDirection = directionToReference.normalized; float worldUpAmount = Vector3.Dot(Vector3.up, worldDirection);
		if (worldUpAmount > (threshold * 0.5f) && IsDirectlyOnTop(targetTransform, referenceTransform)) { return "on top of"; }
		if (absUp >= absForward && absUp >= absRight && absUp > threshold) { return (worldUpAmount > 0) ? "above" : "below"; }
		bool forwardSignificant = absForward > threshold; bool rightSignificant = absRight > threshold;
		if (forwardSignificant && rightSignificant) { string fb = (forwardAmount > 0) ? "in front" : "behind"; string lr = (rightAmount > 0) ? "to the right" : "to the left"; return $"{fb} and {lr} of"; }
		else if (forwardSignificant) { return (forwardAmount > 0) ? "in front of" : "behind"; }
		else if (rightSignificant) { return (rightAmount > 0) ? "to the right of" : "to the left of"; }
		else { return "near"; }
	}
	private bool IsDirectlyOnTop(Transform topTransform, Transform bottomTransform)
	{
		Collider topCollider = topTransform.GetComponent<Collider>(); Collider bottomCollider = bottomTransform.GetComponent<Collider>();
		if (topCollider == null || bottomCollider == null) return false;
		Bounds topBounds = topCollider.bounds; Bounds bottomBounds = bottomCollider.bounds;
		bool overlapsHorizontally = topBounds.min.x < bottomBounds.max.x && topBounds.max.x > bottomBounds.min.x && topBounds.min.z < bottomBounds.max.z && topBounds.max.z > bottomBounds.min.z;
		if (!overlapsHorizontally) return false;
		float verticalGap = topBounds.min.y - bottomBounds.max.y;
		return verticalGap >= -onTopVerticalTolerance && verticalGap <= onTopVerticalTolerance;
	}
	void OnDrawGizmosSelected()
	{ /* Gizmo code unchanged */
		if (ownCollider == null) { ownCollider = GetComponent<Collider>(); if (ownCollider == null) return; }
		Gizmos.color = new Color(0f, 1f, 0f, 0.2f); Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one); if (ownCollider is BoxCollider box) { Gizmos.DrawCube(box.center, Vector3.Scale(box.size, transform.lossyScale)); } else if (ownCollider is SphereCollider sphere) { Gizmos.DrawSphere(sphere.center, sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z)); } else { Gizmos.matrix = Matrix4x4.identity; Gizmos.DrawWireCube(ownCollider.bounds.center, ownCollider.bounds.size); }
		Gizmos.matrix = Matrix4x4.identity; if (_objectsInTrigger != null && Application.isPlaying) { Gizmos.color = Color.yellow; var currentValidTriggers = _objectsInTrigger.Where(col => col != null && col.enabled && col.gameObject.activeInHierarchy && IsInCheckLayers(col.gameObject.layer) && !IsInExcludedHierarchy(col.gameObject)).ToList(); foreach (var col in currentValidTriggers) { Gizmos.DrawLine(ownCollider.bounds.center, col.bounds.center); } }
	}
}

