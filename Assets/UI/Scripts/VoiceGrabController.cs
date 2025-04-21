using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // Ensure this namespace is correct
using Meta.WitAi.TTS.Utilities; // Namespace for Meta TTSSpeaker
using System.Text.RegularExpressions;

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

	[Header("Camera")]
	// Reference to the main camera, assign in Inspector or find automatically
	public Camera mainCamera;

	// Map CLEANED, LOWERCASE names to a LIST of grabbable objects sharing that cleaned name
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

		// Find the main camera if not assigned
		if (mainCamera == null)
		{
			mainCamera = Camera.main;
			if (mainCamera == null)
			{
				Debug.LogError("VoiceGrabController: Main Camera could not be found automatically. Please assign it in the Inspector.");
			}
		}

		FindAndMapGrabbableObjects();
		Debug.Log($"Mapped {grabbableObjectsByCleanName.Count} unique names: {string.Join(", ", grabbableObjectsByCleanName.Keys)}");
	}

	// Finds all XRGrabInteractable objects and maps them by their CLEANED, LOWERCASE name
	void FindAndMapGrabbableObjects()
	{
		grabbableObjectsByCleanName.Clear();
		XRGrabInteractable[] allGrabbables = FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None);

		foreach (var grabObj in allGrabbables)
		{
			if (((1 << grabObj.gameObject.layer) & grabbableLayer) == 0) continue;

			string originalName = grabObj.gameObject.name;
			string cleanedName = CleanNameForDictionary(originalName);

			if (string.IsNullOrEmpty(cleanedName) || cleanedName == "unnamed object")
			{
				// Keep this warning minimal as it can be spammy if many objects lack good names
				// Debug.LogWarning($"Object '{originalName}' resulted in an empty or default cleaned name. Skipping.");
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
		// Use "unnamed object" as a fallback key if cleaning results in empty string
		return string.IsNullOrEmpty(cleaned) ? "unnamed object" : cleaned;
	}

	/// <summary>
	/// Cleans dictated input for matching against dictionary keys.
	/// Converts to lowercase and trims whitespace.
	/// </summary>
	private string CleanDictatedInput(string dictatedInput)
	{
		if (string.IsNullOrEmpty(dictatedInput)) return string.Empty;
		return dictatedInput.ToLowerInvariant().Trim();
	}

	/// <summary>
	/// Finds the best matching CLEANED object name from the dictionary based on the dictated input.
	/// Prioritizes the longest match where the cleaned dictated input STARTS WITH a known cleaned object name.
	/// </summary>
	private string FindBestMatchingCleanedName(string dictatedInput)
	{
		string cleanedInput = CleanDictatedInput(dictatedInput);
		if (string.IsNullOrEmpty(cleanedInput)) return null;

		string bestMatch = null;
		int longestMatchLength = 0;

		foreach (string knownCleanedName in grabbableObjectsByCleanName.Keys)
		{
			// Skip the placeholder key if it exists
			if (knownCleanedName == "unnamed object") continue;

			if (cleanedInput.StartsWith(knownCleanedName, StringComparison.Ordinal))
			{
				if (knownCleanedName.Length > longestMatchLength)
				{
					longestMatchLength = knownCleanedName.Length;
					bestMatch = knownCleanedName;
				}
			}
		}
		return bestMatch;
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
	/// If multiple objects share the best matching name, grabs the closest available one within maxGrabDistance to either hand.
	/// </summary>
	public void GrabObjectByName(string dictatedInput)
	{
		string targetCleanedName = FindBestMatchingCleanedName(dictatedInput);

		if (targetCleanedName == null)
		{
			Debug.LogWarning($"Voice Grab: Cannot find any known object matching the start of '{dictatedInput}'.");
			SpeakIfAvailable($"Sorry, I didn't find an object matching {dictatedInput}.");
			return;
		}

		if (!grabbableObjectsByCleanName.TryGetValue(targetCleanedName, out List<XRGrabInteractable> candidates))
		{
			Debug.LogError($"Voice Grab: Internal error. Found key '{targetCleanedName}' but no corresponding list.");
			SpeakIfAvailable("Sorry, something went wrong.");
			return;
		}

		List<XRGrabInteractable> availableCandidates = candidates
			.Where(c => c != null && c.gameObject != null && c != heldObjectLeft && c != heldObjectRight)
			.ToList();

		if (availableCandidates.Count == 0)
		{
			bool anyExist = candidates.Any(c => c != null && c.gameObject != null);
			if (anyExist)
			{
				Debug.LogWarning($"Voice Grab: All available objects named '{targetCleanedName}' are already held.");
				SpeakIfAvailable($"Already holding {targetCleanedName}.");
			}
			else
			{
				Debug.LogWarning($"Voice Grab: Found match for '{targetCleanedName}', but no valid objects currently exist in the scene.");
				SpeakIfAvailable($"Cannot find {targetCleanedName} right now.");
			}
			return;
		}

		XRGrabInteractable closestCandidate = null;
		float minDistanceSq = float.MaxValue;
		float maxDistSq = maxGrabDistance * maxGrabDistance;
		Vector3 leftHandPos = leftHandController.position;
		Vector3 rightHandPos = rightHandController.position;

		foreach (var candidate in availableCandidates)
		{
			if (candidate == null || candidate.transform == null) continue;
			Vector3 candidatePos = candidate.transform.position;
			float distSqLeft = (leftHandPos - candidatePos).sqrMagnitude;
			float distSqRight = (rightHandPos - candidatePos).sqrMagnitude;
			float closerDistSq = Mathf.Min(distSqLeft, distSqRight);

			if (closerDistSq <= maxDistSq && closerDistSq < minDistanceSq)
			{
				minDistanceSq = closerDistSq;
				closestCandidate = candidate;
			}
		}

		if (closestCandidate == null)
		{
			Debug.LogWarning($"Voice Grab: Object matching '{targetCleanedName}' found, but all available instances are too far away (Max Distance: {maxGrabDistance}).");
			SpeakIfAvailable($"{targetCleanedName} is too far away.");
			return;
		}

		float finalDistLeft = (leftHandPos - closestCandidate.transform.position).sqrMagnitude;
		float finalDistRight = (rightHandPos - closestCandidate.transform.position).sqrMagnitude;
		bool preferLeft = finalDistLeft <= finalDistRight;

		if (preferLeft)
		{
			if (heldObjectLeft == null) PerformGrab(closestCandidate, leftHandController, ref heldObjectLeft, ref heldObjectRbLeft, ref originalParentLeft);
			else if (heldObjectRight == null) PerformGrab(closestCandidate, rightHandController, ref heldObjectRight, ref heldObjectRbRight, ref originalParentRight);
			else SpeakIfAvailable("Both hands are full.");
		}
		else
		{
			if (heldObjectRight == null) PerformGrab(closestCandidate, rightHandController, ref heldObjectRight, ref heldObjectRbRight, ref originalParentRight);
			else if (heldObjectLeft == null) PerformGrab(closestCandidate, leftHandController, ref heldObjectLeft, ref heldObjectRbLeft, ref originalParentLeft);
			else SpeakIfAvailable("Both hands are full.");
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
		else
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
			if (heldCleanedLeft.Equals(targetCleanedName, StringComparison.Ordinal))
			{
				PerformRelease(leftHandController, ref heldObjectLeft, ref heldObjectRbLeft, ref originalParentLeft);
				releasedSomething = true;
			}
		}

		// Check Right Hand (only if left didn't match or wasn't holding the target)
		if (heldObjectRight != null && !releasedSomething)
		{
			string heldCleanedRight = CleanNameForDictionary(heldObjectRight.gameObject.name);
			if (heldCleanedRight.Equals(targetCleanedName, StringComparison.Ordinal))
			{
				PerformRelease(rightHandController, ref heldObjectRight, ref heldObjectRbRight, ref originalParentRight);
				releasedSomething = true;
			}
		}
		// Check Right Hand again if left hand was released, in case user wants to release all matching objects
		else if (heldObjectRight != null && releasedSomething && heldObjectLeft == null)
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
			SpeakIfAvailable($"Not currently holding {targetCleanedName}.");
		}
	}

	/// <summary>
	/// Releases the object held in the specified hand ("left" or "right").
	/// </summary>
	public void DropItem(string hand)
	{
		string lowerHand = hand.ToLowerInvariant().Trim();

		if (lowerHand == "left")
		{
			if (heldObjectLeft != null) PerformRelease(leftHandController, ref heldObjectLeft, ref heldObjectRbLeft, ref originalParentLeft);
			else SpeakIfAvailable("Left hand empty.");
		}
		else if (lowerHand == "right")
		{
			if (heldObjectRight != null) PerformRelease(rightHandController, ref heldObjectRight, ref heldObjectRbRight, ref originalParentRight);
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

		// Use "unnamed object" check if CleanNameForDictionary returns it
		bool holdingLeft = !string.IsNullOrEmpty(leftItemName) && leftItemName != "unnamed object";
		bool holdingRight = !string.IsNullOrEmpty(rightItemName) && rightItemName != "unnamed object";

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
			// Use SpeakQueued to prevent interrupting ongoing speech
			ttsSpeaker.SpeakQueued(textToSpeak);
		}
		else if (ttsSpeaker == null)
		{
			Debug.LogError("TTSSpeaker not assigned. Cannot speak.");
		}
	}

	// --- Internal Grab/Release Logic ---
	private void PerformGrab(XRGrabInteractable interactable, Transform hand, ref XRGrabInteractable heldObjectRef, ref Rigidbody heldObjectRbRef, ref Transform originalParentRef)
	{
		if (interactable == null) return;

		heldObjectRef = interactable;
		originalParentRef = interactable.transform.parent;
		heldObjectRbRef = interactable.GetComponent<Rigidbody>();
		if (heldObjectRbRef != null)
		{
			heldObjectRbRef.isKinematic = true;
			heldObjectRbRef.interpolation = RigidbodyInterpolation.None;
		}
		interactable.transform.SetParent(hand);
		interactable.transform.localPosition = Vector3.zero;
		interactable.transform.localRotation = Quaternion.identity;

		string cleanedName = CleanNameForDictionary(interactable.name);
		Debug.Log($"Voice Grab: {hand.name} grabbed {cleanedName} (Actual: {interactable.name})");
		SpeakIfAvailable($"Grabbed {cleanedName}.");
	}

	private void PerformRelease(Transform hand, ref XRGrabInteractable heldObjectRef, ref Rigidbody heldObjectRbRef, ref Transform originalParentRef)
	{
		if (heldObjectRef == null) return;

		XRGrabInteractable interactableToRelease = heldObjectRef;
		Rigidbody rbToRelease = heldObjectRbRef;
		Transform parentToRestore = originalParentRef;

		string actualName = interactableToRelease.name;
		string cleanedName = CleanNameForDictionary(actualName);
		Debug.Log($"Voice Release: {hand.name} releasing {cleanedName} (Actual: {actualName})");

		interactableToRelease.transform.SetParent(parentToRestore);

		if (rbToRelease != null)
		{
			rbToRelease.isKinematic = false;
			rbToRelease.linearVelocity = Vector3.zero;
			rbToRelease.angularVelocity = Vector3.zero;
		}

		heldObjectRef = null;
		heldObjectRbRef = null;
		originalParentRef = null;

		SpeakIfAvailable($"Released {cleanedName}.");
	}

	// --- MODIFIED FUNCTION ---
	/// <summary>
	/// Calculates the distance from the main camera to the closest object
	/// matching the beginning of the dictated input. Also speaks the result using TTS.
	/// </summary>
	/// <param name="dictatedInput">The voice input potentially containing the object name.</param>
	/// <returns>The distance to the closest matching object, or -1f if not found or camera is missing.</returns>
	public void  GetObjectDistanceToCamera(string dictatedInput)
	{
		if (mainCamera == null)
		{
			Debug.LogError("GetObjectDistanceToCamera: Main Camera is not set or found.");
			SpeakIfAvailable("I can't locate the object without the main camera."); // Added TTS feedback
			return	;
		}

		string targetCleanedName = FindBestMatchingCleanedName(dictatedInput);

		if (targetCleanedName == null)
		{
			Debug.LogWarning($"GetObjectDistanceToCamera: Cannot find any known object matching the start of '{dictatedInput}'.");
			SpeakIfAvailable($"Sorry, I don't know what {dictatedInput} is."); // Added TTS feedback
			return;
		}

		if (!grabbableObjectsByCleanName.TryGetValue(targetCleanedName, out List<XRGrabInteractable> candidates))
		{
			Debug.LogError($"GetObjectDistanceToCamera: Internal error. Found key '{targetCleanedName}' but no corresponding list.");
			SpeakIfAvailable("Sorry, something went wrong finding that object."); // Added TTS feedback
			return;
		}

		List<XRGrabInteractable> validCandidates = candidates
			.Where(c => c != null && c.gameObject != null)
			.ToList();

		if (validCandidates.Count == 0)
		{
			Debug.LogWarning($"GetObjectDistanceToCamera: Found match for '{targetCleanedName}', but no valid objects currently exist in the scene.");
			SpeakIfAvailable($"I can't find any {targetCleanedName} right now."); // Added TTS feedback
			return;
		}

		XRGrabInteractable closestCandidate = null;
		float minDistanceSq = float.MaxValue;
		Vector3 cameraPosition = mainCamera.transform.position;

		foreach (var candidate in validCandidates)
		{
			if (candidate == null || candidate.transform == null) continue;
			Vector3 candidatePos = candidate.transform.position;
			float distSq = (cameraPosition - candidatePos).sqrMagnitude;
			if (distSq < minDistanceSq)
			{
				minDistanceSq = distSq;
				closestCandidate = candidate;
			}
		}

		if (closestCandidate == null)
		{
			Debug.LogError("GetObjectDistanceToCamera: Failed to determine closest candidate despite having valid options.");
			SpeakIfAvailable("Sorry, I had trouble finding the closest object."); // Added TTS feedback
			return;
		}

		// --- Calculate Distance and Direction ---
		Vector3 closestObjectPosition = closestCandidate.transform.position;
		float actualDistance = Vector3.Distance(cameraPosition, closestObjectPosition);
		string objectActualCleanedName = CleanNameForDictionary(closestCandidate.name); // Get specific name

		// Calculate relative direction
		Vector3 directionToObject = closestObjectPosition - cameraPosition;
		Vector3 directionHorizontal = Vector3.ProjectOnPlane(directionToObject, Vector3.up).normalized; // Project to XZ plane and normalize
		string directionString = "nearby"; // Default

		// Check if the horizontal direction is valid (not directly above/below)
		if (directionHorizontal.sqrMagnitude > 0.001f) // Avoid issues with zero vector
		{
			Vector3 cameraForwardHorizontal = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
			float angle = Vector3.SignedAngle(cameraForwardHorizontal, directionHorizontal, Vector3.up);

			// Determine direction string based on angle
			if (angle >= -45f && angle < 45f)
			{
				directionString = "in front of you";
			}
			else if (angle >= 45f && angle < 135f)
			{
				directionString = "to your right";
			}
			else if (angle < -45f && angle >= -135f)
			{
				directionString = "to your left";
			}
			else // Angle > 135f or < -135f
			{
				directionString = "behind you";
			}
		}
		else
		{
			// Handle cases directly above or below if needed, otherwise default works
			// For now, keep the "nearby" default or refine if needed
			// Example: Check directionToObject.y to determine above/below
			if (directionToObject.y > 0) directionString = "above you";
			else if (directionToObject.y < 0) directionString = "below you";
			// If keeping simple 4 directions, default to "in front" might be better than "nearby"
			// directionString = "in front of you";
		}


		// --- Speak the result ---
		string distanceString = actualDistance.ToString("F1"); // Format distance to 1 decimal place
		string message = $"{objectActualCleanedName} is {distanceString} meters {directionString}.";
		SpeakIfAvailable(message);

		Debug.Log($"GetObjectDistanceToCamera: Closest object matching '{targetCleanedName}' is '{closestCandidate.name}' ({objectActualCleanedName}) at {actualDistance} units, direction: {directionString}. Angle: {Vector3.SignedAngle(Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized, directionHorizontal, Vector3.up):F1}"); // Added angle to log

		return	;
	}

}
