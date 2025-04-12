using UnityEngine;
using Oculus.Voice;       // Namespace for AppVoiceExperience
using UnityEngine.Events; // For UnityEvents
using System;             // For StringComparison
using System.Collections; // Required for Coroutines
using System.Collections.Generic; // To use List<>

// Define a helper class to pair a trigger word with a UnityEvent
// This makes it easy to configure in the Inspector
[System.Serializable]
public class WordActionPair
{
	[Tooltip("The specific word that should trigger the associated action.")]
	public string triggerWord;
	[Tooltip("The UnityEvent(s) to invoke when the trigger word is detected.")]
	public UnityEvent actionToTrigger;
}

// AssistanceMode script modified to attempt continuous listening with delayed reactivation
public class AssistanceMode : MonoBehaviour
{
	[Tooltip("Reference to the AppVoiceExperience component in your scene.")]
	[SerializeField] private AppVoiceExperience appVoiceExperience;

	[Space]
	[Tooltip("List of trigger words and the actions they trigger.")]
	[SerializeField] private List<WordActionPair> wordActions = new List<WordActionPair>();

	[Tooltip("Make the word matching case-insensitive?")]
	[SerializeField] private bool ignoreCase = true;

	[Tooltip("Delay in seconds before reactivating dictation after it ends.")]
	[SerializeField] private float reactivationDelay = 0.5f; // Adjust as needed

	// Flag to prevent potential rapid reactivation loops if needed
	private bool isActivating = false;
	private Coroutine reactivationCoroutine = null; // Keep track of the coroutine

	void Start()
	{
		if (appVoiceExperience == null)
		{
			Debug.LogError("AppVoiceExperience reference is not set in AssistanceMode script!", this);
			enabled = false; // Disable script if component not found
			return;
		}

		if (wordActions == null || wordActions.Count == 0)
		{
			Debug.LogWarning("AssistanceMode: No trigger words have been configured in the 'Word Actions' list.", this);
		}

		// Subscribe to transcription events
		appVoiceExperience.VoiceEvents.OnFullTranscription.AddListener(HandleTranscription);
		appVoiceExperience.VoiceEvents.OnRequestCompleted.AddListener(HandleDictationEnded);

		Debug.Log($"AssistanceMode initialized. Listening for {wordActions.Count} trigger word(s).");

		// Automatically activate dictation when the script starts
		ActivateDictationService();
	}

	void OnDestroy()
	{
		// Stop any pending reactivation if the object is destroyed
		if (reactivationCoroutine != null)
		{
			StopCoroutine(reactivationCoroutine);
			reactivationCoroutine = null;
		}

		// IMPORTANT: Always unsubscribe from events
		if (appVoiceExperience != null)
		{
			appVoiceExperience.VoiceEvents.OnFullTranscription.RemoveListener(HandleTranscription);
			appVoiceExperience.VoiceEvents.OnRequestCompleted.RemoveListener(HandleDictationEnded);

			// Optionally deactivate if it's still active when destroyed
			if (appVoiceExperience.Active)
			{
				appVoiceExperience.Deactivate();
			}
		}
	}

	// Handles the transcription result
	private void HandleTranscription(string transcript)
	{
		if (string.IsNullOrEmpty(transcript) || wordActions == null || wordActions.Count == 0)
		{
			return; // Ignore empty results or if no actions are configured
		}

		Debug.Log($"Transcription received: '{transcript}'");

		StringComparison comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
		bool wordFound = false;

		// Iterate through each configured word-action pair
		foreach (WordActionPair pair in wordActions)
		{
			// Skip if the trigger word in the pair is empty or null
			if (string.IsNullOrEmpty(pair.triggerWord))
			{
				continue;
			}

			// Check if the transcript contains the current trigger word
			if (transcript.IndexOf(pair.triggerWord, comparison) >= 0)
			{
				Debug.Log($"Target word '{pair.triggerWord}' DETECTED in transcript!");
				wordFound = true;

				// Invoke the specific UnityEvent associated with this word
				pair.actionToTrigger?.Invoke();

				// Note: This current logic will trigger actions for *all* matching words found
				// in a single transcript. If you only want the *first* match to trigger,
				// you could add 'break;' here after the Invoke().
			}
		}

		if (!wordFound)
		{
			Debug.Log($"No configured trigger words found in the transcript.");
		}
	}

	// --- Example function that could be linked to a UnityEvent ---
	public void ExampleAction_ActivateHelp()
	{
		// Check if the GameObject this script component is attached to is active in the hierarchy
		if (!this.gameObject.activeSelf)
		{
			Debug.Log($"ExampleAction_ActivateHelp called on inactive GameObject '{this.gameObject.name}'. Skipping action.");
			return; // Stop execution if the GameObject is not active
		}
		Debug.LogWarning("ASSISTANCE MODE: ExampleAction_ActivateHelp() HAS BEEN TRIGGERED!");
		GameObject helpPanel = GameObject.Find("HelpPanel"); // Example: Find a UI panel
		if (helpPanel != null)
		{
			helpPanel.SetActive(!helpPanel.activeSelf); // Toggle visibility
		}
	}

	public void ExampleAction_ChangeColor()
	{
		// Check if the GameObject this script component is attached to is active in the hierarchy
		if (!this.gameObject.activeSelf)
		{
			Debug.Log($"ExampleAction_ChangeColor called on inactive GameObject '{this.gameObject.name}'. Skipping action.");
			return; // Stop execution if the GameObject is not active
		}
		Debug.LogWarning("ASSISTANCE MODE: ExampleAction_ChangeColor() HAS BEEN TRIGGERED!");
		GameObject cube = GameObject.Find("MyCube"); // Example: Find a cube
		if (cube != null)
		{
			Renderer rend = cube.GetComponent<Renderer>();
			if (rend != null)
			{
				rend.material.color = UnityEngine.Random.ColorHSV();
			}
		}
	}

	// --- Event Handler for Dictation Ending ---
	private void HandleDictationEnded()
	{
		Debug.Log("Dictation request ended (completed/timeout). Scheduling reactivation...");
		// Reset activation flag immediately, as we are now handling the completion.
		isActivating = false;

		// Stop any previous reactivation coroutine if it was somehow still running
		if (reactivationCoroutine != null)
		{
			StopCoroutine(reactivationCoroutine);
		}

		// Start the coroutine to reactivate after a delay
		reactivationCoroutine = StartCoroutine(ReactivateAfterDelay());
	}

	// --- Coroutine to reactivate after a delay ---
	private IEnumerator ReactivateAfterDelay()
	{
		Debug.Log($"Waiting for {reactivationDelay} seconds before reactivating...");
		yield return new WaitForSeconds(reactivationDelay);

		Debug.Log("Delay finished. Attempting reactivation.");
		reactivationCoroutine = null; // Clear the coroutine reference
		ActivateDictationService(); // Attempt to activate again
	}


	// --- Central method to activate dictation ---
	private void ActivateDictationService()
	{
		// Add more detailed logging here
		Debug.Log($"Attempting ActivateDictationService. Is AppVoiceExperience null? {appVoiceExperience == null}. Is Active? {appVoiceExperience?.Active}. Is Activating flag set? {isActivating}");

		// Check if AppVoiceExperience is assigned and not already active or activating
		if (appVoiceExperience != null && !appVoiceExperience.Active && !isActivating)
		{
			Debug.Log("Activating dictation service...");
			isActivating = true; // Set flag to prevent immediate reactivation attempts
			appVoiceExperience.Activate();
			Debug.Log("appVoiceExperience.Activate() called.");
			// Note: isActivating flag will be reset when HandleDictationEnded is called next time.
		}
		else if (appVoiceExperience == null)
		{
			Debug.LogError("Cannot activate dictation, AppVoiceExperience is not assigned in AssistanceMode.");
		}
		else if (appVoiceExperience.Active)
		{
			Debug.Log("Dictation service is already active. No need to activate.");
			// It was already active, so ensure isActivating is false in case state was inconsistent.
			isActivating = false;
		}
		else if (isActivating)
		{
			Debug.Log("Dictation service is already in the process of activating (isActivating flag is true). Waiting for completion.");
		}
	}

	// --- Public method retained for potential manual activation (e.g., via button) ---
	public void ManualStartDictation()
	{
		ActivateDictationService();
	}
}
