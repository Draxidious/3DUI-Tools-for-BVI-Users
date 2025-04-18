using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
 // Ensure this namespace is correct for your XRI version
										  // If using newer XRI versions (2.0+), you might need these:
										  // using UnityEngine.XR.Interaction.Toolkit.Interactables;
										  // using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Meta.WitAi.TTS.Utilities; // Namespace for Meta TTSSpeaker
using System.Text.RegularExpressions;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class VoiceGrabController : MonoBehaviour
{
	// Regex to remove Unity's clone numbers like " (1)" or trailing numbers AFTER converting to lowercase
	private static readonly Regex nameCleaningRegex = new Regex(@"(\s*\(\d+\)|\d+)$", RegexOptions.Compiled);

	[Header("Hand Controllers")]
	public Transform leftHandController;
	public Transform rightHandController;

	[Header("Grab Settings")]
	public LayerMask grabbableLayer = ~0; // Default to everything if not set
	public float maxGrabDistance = 3.0f; // Maximum distance an object can be grabbed from

	[Header("TTS Settings")]
	public TTSSpeaker ttsSpeaker;

	// Map CLEANED, LOWERCASE names to a LIST of grabbable objects sharing that cleaned name
	// Key: lowercase, trimmed name without clone numbers (e.g., "red cube")
	// Value: List of objects matching that key
	public Dictionary<string, List<XRGrabInteractable>> grabbableObjectsByCleanName = new Dictionary<string, List<XRGrabInteractable>>();

	// Track held objects
	private XRGrabInteractable heldObjectLeft = null;
	private Rigidbody heldObjectRbLeft = null;
	private Transform originalParentLeft = null;

	private XRGrabInteractable heldObjectRight = null;
	private Rigidbody heldObjectRbRight = null;
	private Transform originalParentRight = null;

	void Start()
	{
		if (ttsSpeaker == null)
		{
			Debug.LogError("VoiceGrabController: TTSSpeaker component has not been assigned!");
		}
		// Ensure maxGrabDistance is positive
		if (maxGrabDistance < 0)
		{
			maxGrabDistance = 0;
			Debug.LogWarning("Max Grab Distance cannot be negative. Setting to 0.");
		}
		FindAndMapGrabbableObjects();
		Debug.LogWarning(grabbableObjectsByCleanName.ToString());
	}

	// Finds all XRGrabInteractable objects and maps them by their CLEANED, LOWERCASE name
	void FindAndMapGrabbableObjects()
	{
		grabbableObjectsByCleanName.Clear();
		// FindObjectsOfType is deprecated, use FindObjectsByType in newer Unity versions
		// XRGrabInteractable[] allGrabbables = FindObjectsOfType<XRGrabInteractable>(true);
		XRGrabInteractable[] allGrabbables = FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None); // Includes inactive if needed: FindObjectsInactive.Include

		foreach (var grabObj in allGrabbables)
		{
			// Check if the object is on the specified grabbable layer
			if (((1 << grabObj.gameObject.layer) & grabbableLayer) == 0)
			{
				continue; // Skip if not on the correct layer
			}

			string originalName = grabObj.gameObject.name;
			string cleanedName = CleanNameForDictionary(originalName); // Use specific cleaning for dictionary keys

			if (string.IsNullOrEmpty(cleanedName)) // Skip objects with no usable name after cleaning
			{
				Debug.LogWarning($"Object '{originalName}' resulted in an empty cleaned name. Skipping.");
				continue;
			}

			if (grabbableObjectsByCleanName.TryGetValue(cleanedName, out List<XRGrabInteractable> objectList))
			{
				objectList.Add(grabObj);
			}
			else
			{
				grabbableObjectsByCleanName.Add(cleanedName, new List<XRGrabInteractable> { grabObj });
			}
		}

		Debug.Log($"Mapped {grabbableObjectsByCleanName.Count} unique cleaned names to grabbable objects (considering layer mask).");
		if (grabbableObjectsByCleanName.Count == 0)
		{
			Debug.LogWarning("No XRGrabInteractable objects found matching the criteria (check layer mask and object names).");
		}
	}

	/// <summary>
	/// Cleans the object name for use as a dictionary key.
	/// Converts to lowercase, removes clone numbers, and trims whitespace.
	/// </summary>
	private string CleanNameForDictionary(string originalName)
	{
		if (string.IsNullOrEmpty(originalName)) return string.Empty;

		string lowerName = originalName.ToLowerInvariant();
		string cleaned = nameCleaningRegex.Replace(lowerName, "").Trim();
		// Prevent returning empty strings which are invalid dictionary keys
		return string.IsNullOrEmpty(cleaned) ? "unnamed object" : cleaned;
	}

	/// <summary>
	/// Cleans dictated input for matching against dictionary keys.
	/// Converts to lowercase and trims whitespace. Does NOT remove numbers here,
	/// as the dictation might intentionally include them (e.g., "block 1").
	/// </summary>
	private string CleanDictatedInput(string dictatedInput)
	{
		if (string.IsNullOrEmpty(dictatedInput)) return string.Empty;
		return dictatedInput.ToLowerInvariant().Trim();
	}

	/// <summary>
	/// Finds the best matching CLEANED object name from the dictionary based on the dictated input.
	/// It prioritizes the longest match where the cleaned dictated input STARTS WITH a known cleaned object name.
	/// </summary>
	/// <param name="dictatedInput">The raw string from voice dictation.</param>
	/// <returns>The best matching cleaned name key from the dictionary, or null if no match found.</returns>
	private string FindBestMatchingCleanedName(string dictatedInput)
	{
		string cleanedInput = CleanDictatedInput(dictatedInput);
		if (string.IsNullOrEmpty(cleanedInput)) return null;

		string bestMatch = null;
		int longestMatchLength = 0;

		// Iterate through all known cleaned names (dictionary keys)
		foreach (string knownCleanedName in grabbableObjectsByCleanName.Keys)
		{
			// Check if the cleaned dictated input *starts with* a known object name
			if (cleanedInput.StartsWith(knownCleanedName, StringComparison.Ordinal)) // Ordinal is efficient for known-case comparison
			{
				// If this match is longer than the current best match, update
				if (knownCleanedName.Length > longestMatchLength)
				{
					longestMatchLength = knownCleanedName.Length;
					bestMatch = knownCleanedName;
				}
			}
		}

		return bestMatch; // This will be null if no starting match was found
	}


	/// <summary>
	/// Checks if any known grabbable object name matches the start of the dictated input.
	/// </summary>
	public bool IsObjectGrabbable(string dictatedInput)
	{
		string bestMatch = FindBestMatchingCleanedName(dictatedInput);
		return bestMatch != null;
	}


	/// <summary>
	/// Grabs an object based on voice input. Matches the beginning of the input against known object names.
	/// If multiple objects share the best matching name, grabs the closest available one within maxGrabDistance.
	/// </summary>
	public void GrabObjectByName(string dictatedInput)
	{
		string targetCleanedName = FindBestMatchingCleanedName(dictatedInput);

		if (targetCleanedName == null)
		{
			Debug.LogWarning($"Voice Grab: Cannot find any known object matching the start of '{dictatedInput}'.");
			ttsSpeaker.SpeakQueued($"Sorry, I didn't find an object matching {dictatedInput}.");
			return;
		}

		// We found a matching key, now get the list of actual objects
		if (!grabbableObjectsByCleanName.TryGetValue(targetCleanedName, out List<XRGrabInteractable> candidates))
		{
			// This should theoretically not happen if FindBestMatchingCleanedName returned a non-null key
			Debug.LogError($"Voice Grab: Internal error. Found key '{targetCleanedName}' but no corresponding list.");
			ttsSpeaker.SpeakQueued("Sorry, something went wrong.");
			return;
		}

		// Filter out candidates that are already held
		List<XRGrabInteractable> availableCandidates = candidates
			.Where(c => c != null && c.gameObject != null && c != heldObjectLeft && c != heldObjectRight)
			.ToList();

		if (availableCandidates.Count == 0)
		{
			// Check if it's because they are held or because the objects were destroyed/disabled
			bool anyExist = candidates.Any(c => c != null && c.gameObject != null);
			if (anyExist)
			{
				Debug.LogWarning($"Voice Grab: All available objects named '{targetCleanedName}' are already held.");
				ttsSpeaker.SpeakQueued($"Already holding {targetCleanedName}.");
			}
			else
			{
				Debug.LogWarning($"Voice Grab: Found match for '{targetCleanedName}', but no valid objects currently exist in the scene (perhaps destroyed or inactive?).");
				ttsSpeaker.SpeakQueued($"Cannot find {targetCleanedName} right now.");
				// Optional: Refresh the map if objects might be dynamically added/removed
				// FindAndMapGrabbableObjects();
			}
			return;
		}

		// Find the closest available candidate *within max distance* to either hand
		XRGrabInteractable closestCandidate = null;
		float minDistanceSq = float.MaxValue;
		float maxDistSq = maxGrabDistance * maxGrabDistance; // Calculate squared max distance once

		Vector3 leftHandPos = leftHandController.position;
		Vector3 rightHandPos = rightHandController.position;

		foreach (var candidate in availableCandidates)
		{
			// Ensure candidate is not null before accessing transform
			if (candidate == null || candidate.transform == null) continue;

			Vector3 candidatePos = candidate.transform.position;
			float distSqLeft = (leftHandPos - candidatePos).sqrMagnitude;
			float distSqRight = (rightHandPos - candidatePos).sqrMagnitude;
			float closerDistSq = Mathf.Min(distSqLeft, distSqRight);

			// Check if this candidate is within max distance AND closer than the current best
			if (closerDistSq <= maxDistSq && closerDistSq < minDistanceSq)
			{
				minDistanceSq = closerDistSq;
				closestCandidate = candidate;
			}
		}

		// Check if a suitable candidate was found within range
		if (closestCandidate == null)
		{
			Debug.LogWarning($"Voice Grab: Object matching '{targetCleanedName}' found, but all available instances are too far away (Max Distance: {maxGrabDistance}).");
			ttsSpeaker.SpeakQueued($"{targetCleanedName} is too far away.");
			return;
		}

		// Determine which hand is closer to the chosen closest candidate
		// Recalculate final distances to the chosen candidate
		float finalDistLeft = (leftHandPos - closestCandidate.transform.position).sqrMagnitude;
		float finalDistRight = (rightHandPos - closestCandidate.transform.position).sqrMagnitude;
		bool preferLeft = finalDistLeft <= finalDistRight;

		// Attempt grab with the preferred hand, fallback to the other if busy
		if (preferLeft)
		{
			if (heldObjectLeft == null) PerformGrab(closestCandidate, leftHandController, ref heldObjectLeft, ref heldObjectRbLeft, ref originalParentLeft);
			else if (heldObjectRight == null) PerformGrab(closestCandidate, rightHandController, ref heldObjectRight, ref heldObjectRbRight, ref originalParentRight);
			else ttsSpeaker.SpeakQueued("Both hands are full.");
		}
		else // Prefer Right
		{
			if (heldObjectRight == null) PerformGrab(closestCandidate, rightHandController, ref heldObjectRight, ref heldObjectRbRight, ref originalParentRight);
			else if (heldObjectLeft == null) PerformGrab(closestCandidate, leftHandController, ref heldObjectLeft, ref heldObjectRbLeft, ref originalParentLeft);
			else ttsSpeaker.SpeakQueued("Both hands are full.");
		}
	}

	/// <summary>
	/// Releases the object held by the specified hand.
	/// </summary>
	public void ReleaseHeldObject(bool releaseLeftHand)
	{
		if (releaseLeftHand)
		{
			if (heldObjectLeft != null) PerformRelease(leftHandController, ref heldObjectLeft, ref heldObjectRbLeft, ref originalParentLeft);
			else SpeakIfAvailable("Left hand is empty.");
		}
		else // Release Right Hand
		{
			if (heldObjectRight != null) PerformRelease(rightHandController, ref heldObjectRight, ref heldObjectRbRight, ref originalParentRight);
			else SpeakIfAvailable("Right hand is empty.");
		}
	}

	/// <summary>
	/// Releases any object held in either hand whose cleaned name matches the start of the input.
	/// </summary>
	public void ReleaseObjectByName(string dictatedInput)
	{
		string targetCleanedName = FindBestMatchingCleanedName(dictatedInput);

		if (targetCleanedName == null)
		{
			SpeakIfAvailable($"Sorry, I don't know what {dictatedInput} refers to.");
			Debug.LogWarning($"Voice Release: Cannot find any known object matching the start of '{dictatedInput}'.");
			return;
		}

		bool releasedSomething = false;

		// Check Left Hand
		if (heldObjectLeft != null)
		{
			string heldCleanedLeft = CleanNameForDictionary(heldObjectLeft.gameObject.name);
			// Check if the cleaned name of the held object exactly matches the target cleaned name
			if (heldCleanedLeft.Equals(targetCleanedName, StringComparison.Ordinal))
			{
				PerformRelease(leftHandController, ref heldObjectLeft, ref heldObjectRbLeft, ref originalParentLeft);
				releasedSomething = true;
			}
		}

		// Check Right Hand (only if left didn't match or wasn't holding the target)
		if (heldObjectRight != null && !releasedSomething) // Avoid releasing twice if both hands hold same named object and only one was requested
		{
			string heldCleanedRight = CleanNameForDictionary(heldObjectRight.gameObject.name);
			if (heldCleanedRight.Equals(targetCleanedName, StringComparison.Ordinal))
			{
				PerformRelease(rightHandController, ref heldObjectRight, ref heldObjectRbRight, ref originalParentRight);
				releasedSomething = true;
			}
		}
		// Check Right Hand again if left hand was released, in case user wants to release all matching objects
		else if (heldObjectRight != null && releasedSomething && heldObjectLeft == null) // Check right hand again if left was just released
		{
			string heldCleanedRight = CleanNameForDictionary(heldObjectRight.gameObject.name);
			if (heldCleanedRight.Equals(targetCleanedName, StringComparison.Ordinal))
			{
				PerformRelease(rightHandController, ref heldObjectRight, ref heldObjectRbRight, ref originalParentRight);
				// releasedSomething is already true
			}
		}


		if (!releasedSomething)
		{
			// We know the name exists because targetCleanedName is not null, so they must not be holding it.
			SpeakIfAvailable($"Not currently holding {targetCleanedName}.");
		}
	}

	/// <summary>
	/// Releases the object held in the specified hand ("left" or "right").
	/// Expects simple "left" or "right" input.
	/// </summary>
	public void DropItem(string hand)
	{
		string lowerHand = hand.ToLowerInvariant().Trim();

		if (lowerHand == "left")
		{
			if (heldObjectLeft != null)
			{
				// Release already speaks the name
				PerformRelease(leftHandController, ref heldObjectLeft, ref heldObjectRbLeft, ref originalParentLeft);
			}
			else SpeakIfAvailable("Left hand empty.");
		}
		else if (lowerHand == "right")
		{
			if (heldObjectRight != null)
			{
				// Release already speaks the name
				PerformRelease(rightHandController, ref heldObjectRight, ref heldObjectRbRight, ref originalParentRight);
			}
			else SpeakIfAvailable("Right hand empty.");
		}
		else
		{
			SpeakIfAvailable("Please specify which hand: left or right?");
			Debug.LogWarning($"DropItem Error: Input '{hand}' is not 'left' or 'right'.");
		}
	}

	/// <summary>
	/// Uses the assigned TTSSpeaker to announce what is being held.
	/// </summary>
	public void SpeakHeldItems()
	{
		string leftItemName = heldObjectLeft ? CleanNameForDictionary(heldObjectLeft.gameObject.name) : null;
		string rightItemName = heldObjectRight ? CleanNameForDictionary(heldObjectRight.gameObject.name) : null;

		bool holdingLeft = !string.IsNullOrEmpty(leftItemName);
		bool holdingRight = !string.IsNullOrEmpty(rightItemName);

		string message;

		if (holdingLeft && holdingRight) message = $"Holding {leftItemName} in left hand and {rightItemName} in right hand.";
		else if (holdingLeft) message = $"Holding {leftItemName} in left hand. Right hand is empty.";
		else if (holdingRight) message = $"Holding {rightItemName} in right hand. Left hand is empty.";
		else message = "Both hands are empty.";

		SpeakIfAvailable(message);
	}

	// --- Helper method for TTS ---
	private void SpeakIfAvailable(string textToSpeak)
	{
		if (ttsSpeaker != null && !string.IsNullOrEmpty(textToSpeak))
		{
			ttsSpeaker.SpeakQueued(textToSpeak);
		}
		else if (ttsSpeaker == null)
		{
			Debug.LogError("TTSSpeaker not assigned. Cannot speak.");
		}
	}

	// Internal function to handle the attachment logic
	private void PerformGrab(XRGrabInteractable interactable, Transform hand, ref XRGrabInteractable heldObjectRef, ref Rigidbody heldObjectRbRef, ref Transform originalParentRef)
	{
		if (interactable == null) return;

		heldObjectRef = interactable;
		originalParentRef = interactable.transform.parent; // Store original parent

		heldObjectRbRef = interactable.GetComponent<Rigidbody>();
		if (heldObjectRbRef != null)
		{
			heldObjectRbRef.isKinematic = true; // Make kinematic while held
			heldObjectRbRef.interpolation = RigidbodyInterpolation.None; // Optional: Turn off interpolation while kinematic parented
		}

		// Parent the object to the hand
		interactable.transform.SetParent(hand);

		// Reset local position and rotation relative to the hand
		// You might want to adjust this based on how you want the object oriented in the hand
		interactable.transform.localPosition = Vector3.zero;
		interactable.transform.localRotation = Quaternion.identity;

		// Disable the interactable's ability to be grabbed by standard controllers while voice-held
		// interactable.enabled = false; // Be careful with this, might interfere if you want standard interaction fallback

		string cleanedName = CleanNameForDictionary(interactable.name);
		Debug.Log($"Voice Grab: {hand.name} grabbed {cleanedName} (Actual: {interactable.name})");
		SpeakIfAvailable($"Grabbed {cleanedName}.");
	}

	// Internal function to handle the detachment logic
	private void PerformRelease(Transform hand, ref XRGrabInteractable heldObjectRef, ref Rigidbody heldObjectRbRef, ref Transform originalParentRef)
	{
		if (heldObjectRef == null) return;

		XRGrabInteractable interactableToRelease = heldObjectRef; // Keep reference before nulling
		Rigidbody rbToRelease = heldObjectRbRef;
		Transform parentToRestore = originalParentRef;

		string actualName = interactableToRelease.name;
		string cleanedName = CleanNameForDictionary(actualName); // Use the same cleaning as dictionary
		Debug.Log($"Voice Release: {hand.name} releasing {cleanedName} (Actual: {actualName})");

		// Restore original parent
		interactableToRelease.transform.SetParent(parentToRestore);

		// Restore Rigidbody state
		if (rbToRelease != null)
		{
			rbToRelease.isKinematic = false; // Make non-kinematic again
											 // Optional: Restore interpolation if you changed it
											 // rbToRelease.interpolation = RigidbodyInterpolation.Interpolate;
			rbToRelease.linearVelocity = Vector3.zero; // Reset velocity (or apply throw physics if desired)
			rbToRelease.angularVelocity = Vector3.zero;
		}

		// Re-enable the interactable for standard grabbing if it was disabled
		// interactableToRelease.enabled = true;

		// Clear the references for the hand
		heldObjectRef = null;
		heldObjectRbRef = null;
		originalParentRef = null;

		// Speak AFTER clearing references, using the stored cleaned name
		SpeakIfAvailable($"Released {cleanedName}.");
	}

	
}
