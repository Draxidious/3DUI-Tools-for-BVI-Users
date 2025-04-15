using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Meta.WitAi.TTS.Utilities; // Namespace for Meta TTSSpeaker
using System.Text.RegularExpressions;

public class VoiceGrabController : MonoBehaviour
{
	// Regex to remove Unity's clone numbers like " (1)" or trailing numbers
	private static readonly Regex nameCleaningRegex = new Regex(@"(\s*\(\d+\)|\d+)$", RegexOptions.Compiled);

	[Header("Hand Controllers")]
	public Transform leftHandController;
	public Transform rightHandController;

	[Header("Grab Settings")]
	public LayerMask grabbableLayer;
	public float maxGrabDistance = 3.0f; // Maximum distance an object can be grabbed from

	[Header("TTS Settings")]
	public TTSSpeaker ttsSpeaker;

	// Map CLEANED names to a LIST of grabbable objects sharing that cleaned name
	private Dictionary<string, List<XRGrabInteractable>> grabbableObjectsByCleanName = new Dictionary<string, List<XRGrabInteractable>>();

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
	}

	// Finds all XRGrabInteractable objects and maps them by their CLEANED name
	void FindAndMapGrabbableObjects()
	{
		grabbableObjectsByCleanName.Clear();
		XRGrabInteractable[] allGrabbables = FindObjectsOfType<XRGrabInteractable>(true);

		foreach (var grabObj in allGrabbables)
		{
			if (grabbableLayer != 0 && ((1 << grabObj.gameObject.layer) & grabbableLayer) == 0)
			{
				continue;
			}

			string originalName = grabObj.gameObject.name;
			string cleanedName = CleanName(originalName);

			if (grabbableObjectsByCleanName.TryGetValue(cleanedName, out List<XRGrabInteractable> objectList))
			{
				objectList.Add(grabObj);
			}
			else
			{
				grabbableObjectsByCleanName.Add(cleanedName, new List<XRGrabInteractable> { grabObj });
			}
		}

		Debug.Log($"Mapped {grabbableObjectsByCleanName.Count} unique cleaned names to {allGrabbables.Length} XRGrabInteractable objects.");
		if (grabbableObjectsByCleanName.Count == 0)
		{
			Debug.LogWarning("No XRGrabInteractable objects found matching the criteria (check layer mask?).");
		}
	}

	/// <summary>
	/// Checks if any grabbable object corresponds to the given (potentially uncleaned) name.
	/// </summary>
	public bool IsObjectGrabbable(string objectNameShort)
	{
		string cleanedName = CleanName(objectNameShort);
		return grabbableObjectsByCleanName.ContainsKey(cleanedName);
	}


	/// <summary>
	/// Grabs an object based on its cleaned name. If multiple match, grabs the closest available one within maxGrabDistance.
	/// </summary>
	public void GrabObjectByName(string objectNameShort)
	{
		string cleanedName = CleanName(objectNameShort);

		if (!grabbableObjectsByCleanName.TryGetValue(cleanedName, out List<XRGrabInteractable> candidates))
		{
			Debug.LogError($"Voice Grab: Cannot find any grabbable object with cleaned name '{cleanedName}'.");
			SpeakIfAvailable($"Cannot find {cleanedName}.");
			return;
		}

		List<XRGrabInteractable> availableCandidates = candidates
			.Where(c => c != heldObjectLeft && c != heldObjectRight)
			.ToList();

		if (availableCandidates.Count == 0)
		{
			Debug.LogWarning($"Voice Grab: All objects with cleaned name '{cleanedName}' are already held or none exist.");
			if (candidates.Count > 0) SpeakIfAvailable($"Already holding {cleanedName}.");
			else SpeakIfAvailable($"Cannot find {cleanedName}.");
			return;
		}

		// Find the closest available candidate *within max distance*
		XRGrabInteractable closestCandidate = null;
		float minDistanceSq = float.MaxValue;
		float maxDistSq = maxGrabDistance * maxGrabDistance; // Calculate squared max distance once

		foreach (var candidate in availableCandidates)
		{
			float distSqLeft = (leftHandController.position - candidate.transform.position).sqrMagnitude;
			float distSqRight = (rightHandController.position - candidate.transform.position).sqrMagnitude;
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
			Debug.LogWarning($"Voice Grab: Object '{cleanedName}' found, but all available instances are too far away (Max Distance: {maxGrabDistance}).");
			SpeakIfAvailable($"{cleanedName} is too far away.");
			return;
		}

		// Determine which hand is closer to the chosen closest candidate
		float finalDistLeft = (leftHandController.position - closestCandidate.transform.position).sqrMagnitude;
		float finalDistRight = (rightHandController.position - closestCandidate.transform.position).sqrMagnitude;
		bool preferLeft = finalDistLeft <= finalDistRight;

		// Attempt grab with the preferred hand, fallback to the other if busy
		if (preferLeft)
		{
			if (heldObjectLeft == null) PerformGrab(closestCandidate, leftHandController, ref heldObjectLeft, ref heldObjectRbLeft, ref originalParentLeft);
			else if (heldObjectRight == null) PerformGrab(closestCandidate, rightHandController, ref heldObjectRight, ref heldObjectRbRight, ref originalParentRight);
			else SpeakIfAvailable("Both hands are full.");
		}
		else // Prefer Right
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
		else // Release Right Hand
		{
			if (heldObjectRight != null) PerformRelease(rightHandController, ref heldObjectRight, ref heldObjectRbRight, ref originalParentRight);
			else SpeakIfAvailable("Right hand is empty.");
		}
	}

	/// <summary>
	/// Releases any object held in either hand whose cleaned name matches the input.
	/// </summary>
	public void ReleaseObjectByName(string objectNameShort)
	{
		string cleanedName = CleanName(objectNameShort);
		bool releasedSomething = false;

		if (heldObjectLeft != null && CleanName(heldObjectLeft.gameObject.name).Equals(cleanedName, StringComparison.OrdinalIgnoreCase))
		{
			PerformRelease(leftHandController, ref heldObjectLeft, ref heldObjectRbLeft, ref originalParentLeft);
			releasedSomething = true;
		}

		if (heldObjectRight != null && CleanName(heldObjectRight.gameObject.name).Equals(cleanedName, StringComparison.OrdinalIgnoreCase))
		{
			PerformRelease(rightHandController, ref heldObjectRight, ref heldObjectRbRight, ref originalParentRight);
			releasedSomething = true;
		}

		if (!releasedSomething)
		{
			if (grabbableObjectsByCleanName.ContainsKey(cleanedName)) SpeakIfAvailable($"Not holding {cleanedName}.");
			else SpeakIfAvailable($"Cannot find {cleanedName}.");
		}
	}

	/// <summary>
	/// Releases the object held in the specified hand ("left" or "right").
	/// </summary>
	public void DropItem(string hand)
	{
		string lowerHand = hand.ToLowerInvariant();

		if (lowerHand == "left")
		{
			if (heldObjectLeft != null)
			{
				string itemName = CleanName(heldObjectLeft.gameObject.name);
				PerformRelease(leftHandController, ref heldObjectLeft, ref heldObjectRbLeft, ref originalParentLeft);
				// SpeakIfAvailable($"Dropped {itemName} from left hand."); // Release already speaks
			}
			else SpeakIfAvailable("Left hand empty.");
		}
		else if (lowerHand == "right")
		{
			if (heldObjectRight != null)
			{
				string itemName = CleanName(heldObjectRight.gameObject.name);
				PerformRelease(rightHandController, ref heldObjectRight, ref heldObjectRbRight, ref originalParentRight);
				// SpeakIfAvailable($"Dropped {itemName} from right hand."); // Release already speaks
			}
			else SpeakIfAvailable("Right hand empty.");
		}
		else
		{
			SpeakIfAvailable("Unclear which hand.");
			Debug.LogError("DropItem Error: Please specify 'left' or 'right'.");
		}
	}

	/// <summary>
	/// Uses the assigned TTSSpeaker to announce what is being held.
	/// </summary>
	public void SpeakHeldItems()
	{
		string leftItemName = heldObjectLeft?.gameObject.name;
		string rightItemName = heldObjectRight?.gameObject.name;
		string message = "";

		bool holdingLeft = !string.IsNullOrEmpty(leftItemName);
		bool holdingRight = !string.IsNullOrEmpty(rightItemName);

		string leftClean = holdingLeft ? CleanName(leftItemName) : "";
		string rightClean = holdingRight ? CleanName(rightItemName) : "";

		if (holdingLeft && holdingRight) message = $"Holding {leftClean} in left hand and {rightClean} in right hand.";
		else if (holdingLeft) message = $"Holding {leftClean} in left hand. Right hand is empty.";
		else if (holdingRight) message = $"Holding {rightClean} in right hand. Left hand is empty.";
		else message = "Both hands are empty.";

		SpeakIfAvailable(message);
	}

	// --- Helper method for TTS ---
	private void SpeakIfAvailable(string textToSpeak)
	{
		if (ttsSpeaker != null)
		{
			ttsSpeaker.SpeakQueued(textToSpeak);
		}
		else
		{
			Debug.LogError("TTSSpeaker not assigned. Cannot speak.");
		}
	}

	// Internal function to handle the attachment logic
	private void PerformGrab(XRGrabInteractable interactable, Transform hand, ref XRGrabInteractable heldObjectRef, ref Rigidbody heldObjectRbRef, ref Transform originalParentRef)
	{
		heldObjectRef = interactable;
		originalParentRef = interactable.transform.parent;

		heldObjectRbRef = interactable.GetComponent<Rigidbody>();
		if (heldObjectRbRef != null) heldObjectRbRef.isKinematic = true;

		interactable.transform.SetParent(hand);
		interactable.transform.localPosition = Vector3.zero;
		interactable.transform.localRotation = Quaternion.identity;

		Debug.Log($"Voice Grab: {hand.name} grabbed {CleanName(interactable.name)} (Actual: {interactable.name})");
		SpeakIfAvailable($"Grabbed {CleanName(interactable.name)}.");
	}

	// Internal function to handle the detachment logic
	private void PerformRelease(Transform hand, ref XRGrabInteractable heldObjectRef, ref Rigidbody heldObjectRbRef, ref Transform originalParentRef)
	{
		if (heldObjectRef == null) return;

		string actualName = heldObjectRef.name;
		string cleanedName = CleanName(actualName);
		Debug.Log($"Voice Release: {hand.name} released {cleanedName} (Actual: {actualName})");

		heldObjectRef.transform.SetParent(originalParentRef);

		if (heldObjectRbRef != null)
		{
			heldObjectRbRef.isKinematic = false;
			heldObjectRbRef.linearVelocity = Vector3.zero;
			heldObjectRbRef.angularVelocity = Vector3.zero;
		}

		XRGrabInteractable releasedInteractable = heldObjectRef; // Store reference before nulling

		heldObjectRef = null;
		heldObjectRbRef = null;
		originalParentRef = null;

		// Speak AFTER clearing references, using the stored cleaned name
		SpeakIfAvailable($"Released {cleanedName}.");
	}

	void Update()
	{
		// Parenting handles position updates.
	}

	// Cleans the object name
	private string CleanName(string originalName)
	{
		if (string.IsNullOrEmpty(originalName)) return "object";
		string cleaned = nameCleaningRegex.Replace(originalName, "").Trim();
		return string.IsNullOrEmpty(cleaned) ? "object" : cleaned;
	}
}
