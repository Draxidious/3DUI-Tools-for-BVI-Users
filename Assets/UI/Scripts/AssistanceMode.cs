using UnityEngine;
using Oculus.Voice;       // Namespace for AppVoiceExperience
using UnityEngine.Events; // Required for UnityEvent<T>
using System;             // For StringComparison
using System.Collections; // Required for Coroutines
using System.Collections.Generic; // To use List<>

// --- Structures for Command Configuration ---

// For simple commands WITHOUT parameters
[System.Serializable]
public class SimpleWordActionPair
{
	[Tooltip("The specific word/phrase that acts as the command trigger (no parameter expected).")]
	public string triggerWord;

	[Tooltip("The UnityEvent(s) to invoke when ONLY the trigger word is detected.")]
	public UnityEvent actionToTrigger; // Parameter-less event
}

// For commands WITH parameters
[System.Serializable]
public class ParameterizedWordActionPair
{
	[Tooltip("The specific word/phrase that acts as the command trigger (parameter expected).")]
	public string triggerWord;

	[Tooltip("The UnityEvent(s) to invoke when the trigger word is detected FOLLOWED BY a parameter. The parameter string will be passed.")]
	public UnityEvent<string> actionToTrigger; // Event with string parameter
}


// --- Main Script ---

// AssistanceMode script modified to handle both simple and parameterized commands
public class AssistanceMode : MonoBehaviour
{
	[Tooltip("Reference to the AppVoiceExperience component in your scene.")]
	[SerializeField] private AppVoiceExperience appVoiceExperience;

	[Space]
	[Header("Command Configuration")]

	[Tooltip("Commands that expect a parameter (e.g., 'grab item', 'go location').")]
	[SerializeField] private List<ParameterizedWordActionPair> parameterizedWordActions = new List<ParameterizedWordActionPair>();

	[Tooltip("Simple commands that do not expect a parameter (e.g., 'show help', 'status').")]
	[SerializeField] private List<SimpleWordActionPair> simpleWordActions = new List<SimpleWordActionPair>();


	[Space]
	[Header("Settings")]
	[Tooltip("Make the word matching case-insensitive?")]
	[SerializeField] private bool ignoreCase = true;

	[Tooltip("Delay in seconds before reactivating dictation after it ends.")]
	[SerializeField] private float reactivationDelay = 0.5f; // Adjust as needed

	// --- Private State ---
	private bool isActivating = false;
	private Coroutine reactivationCoroutine = null; // Keep track of the coroutine

	// --- Unity Methods ---

	void Start()
	{
		if (appVoiceExperience == null)
		{
			Debug.LogError("AppVoiceExperience reference is not set in AssistanceMode script!", this);
			enabled = false; // Disable script if component not found
			return;
		}

		// Log warnings if lists are empty
		if ((parameterizedWordActions == null || parameterizedWordActions.Count == 0) &&
			(simpleWordActions == null || simpleWordActions.Count == 0))
		{
			Debug.LogWarning("AssistanceMode: No commands have been configured in either list.", this);
		}

		// Subscribe to transcription events
		appVoiceExperience.VoiceEvents.OnFullTranscription.AddListener(HandleTranscription);
		appVoiceExperience.VoiceEvents.OnRequestCompleted.AddListener(HandleDictationEnded);

		Debug.Log($"AssistanceMode initialized. Listening for {parameterizedWordActions.Count} parameterized and {simpleWordActions.Count} simple command(s).");

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

	// --- Transcription Handling ---

	private void HandleTranscription(string transcript)
	{
		if (string.IsNullOrEmpty(transcript)) return;

		Debug.Log($"Transcription received: '{transcript}'");
		StringComparison comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
		bool commandRecognized = false;

		// 1. Check for Parameterized Commands FIRST
		if (parameterizedWordActions != null)
		{
			foreach (ParameterizedWordActionPair pair in parameterizedWordActions)
			{
				if (string.IsNullOrEmpty(pair.triggerWord)) continue;

				int index = transcript.IndexOf(pair.triggerWord, comparison);
				if (index >= 0)
				{
					bool isStartOfWord = (index == 0) || (index > 0 && char.IsWhiteSpace(transcript[index - 1]));
					if (isStartOfWord)
					{
						int parameterStartIndex = index + pair.triggerWord.Length;
						if (parameterStartIndex < transcript.Length)
						{
							string parameter = transcript.Substring(parameterStartIndex).Trim();
							if (!string.IsNullOrWhiteSpace(parameter))
							{
								Debug.Log($"Parameterized command '{pair.triggerWord}' DETECTED with parameter: '{parameter}'");
								pair.actionToTrigger?.Invoke(parameter);
								commandRecognized = true;
								// Optional: break; // Uncomment if finding one parameterized command should stop checking others
							}
						}
					}
				}
			} // End foreach parameterized
		}

		// 2. Check for Simple Commands if no parameterized command was already recognized
		//    (Or remove this 'if' if simple commands can trigger alongside parameterized ones)
		if (!commandRecognized && simpleWordActions != null)
		{
			foreach (SimpleWordActionPair pair in simpleWordActions)
			{
				if (string.IsNullOrEmpty(pair.triggerWord)) continue;

				int index = transcript.IndexOf(pair.triggerWord, comparison);
				if (index >= 0)
				{
					bool isStartOfWord = (index == 0) || (index > 0 && char.IsWhiteSpace(transcript[index - 1]));
					if (isStartOfWord)
					{
						// Check if nothing (or only whitespace) follows the trigger word
						int endOfTriggerIndex = index + pair.triggerWord.Length;
						bool nothingFollows = endOfTriggerIndex >= transcript.Length;
						bool onlyWhitespaceFollows = !nothingFollows && string.IsNullOrWhiteSpace(transcript.Substring(endOfTriggerIndex));

						if (nothingFollows || onlyWhitespaceFollows)
						{
							Debug.Log($"Simple command '{pair.triggerWord}' DETECTED.");
							pair.actionToTrigger?.Invoke();
							commandRecognized = true;
							// Optional: break; // Uncomment if finding one simple command should stop checking others
						}
					}
				}
			} // End foreach simple
		}


		if (!commandRecognized)
		{
			Debug.Log($"No configured commands recognized in the transcript.");
		}
	}

	// --- Example Action Methods ---

	// Example for Parameterized Command List
	public void Example_GrabItem(string item)
	{
		if (!this.gameObject.activeSelf) return;
		Debug.LogWarning($"ASSISTANCE MODE: Example_GrabItem TRIGGERED with item: '{item}'");
		// Add logic to grab the specified item...
	}

	// Example for Parameterized Command List
	public void Example_ChangeColor(string colorName)
	{
		if (!this.gameObject.activeSelf) return;
		Debug.LogWarning($"ASSISTANCE MODE: Example_ChangeColor TRIGGERED with color: '{colorName}'");
		GameObject cube = GameObject.Find("MyCube");
		if (cube != null)
		{
			Renderer rend = cube.GetComponent<Renderer>();
			if (rend != null)
			{
				Color targetColor = Color.white; // Default
				switch (colorName.ToLowerInvariant())
				{
					case "red": targetColor = Color.red; break;
					case "blue": targetColor = Color.blue; break;
					case "green": targetColor = Color.green; break;
					default: Debug.LogWarning($"Color parameter '{colorName}' not recognized."); break;
				}
				rend.material.color = targetColor;
			}
		}
	}

	// Example for Simple Command List
	public void Example_ShowHelp()
	{
		if (!this.gameObject.activeSelf) return;
		Debug.LogWarning($"ASSISTANCE MODE: Example_ShowHelp TRIGGERED (no parameters).");
		GameObject helpPanel = GameObject.Find("HelpPanel");
		if (helpPanel != null)
		{
			helpPanel.SetActive(!helpPanel.activeSelf);
		}
	}

	// Example for Simple Command List
	public void Example_ReportStatus()
	{
		if (!this.gameObject.activeSelf) return;
		Debug.LogWarning($"ASSISTANCE MODE: Example_ReportStatus TRIGGERED (no parameters).");
		// Add logic to report status...
	}


	// --- Dictation Activation/Reactivation Logic ---

	private void HandleDictationEnded()
	{
		Debug.Log("Dictation request ended. Scheduling reactivation...");
		isActivating = false;
		if (reactivationCoroutine != null) StopCoroutine(reactivationCoroutine);
		reactivationCoroutine = StartCoroutine(ReactivateAfterDelay());
	}

	private IEnumerator ReactivateAfterDelay()
	{
		Debug.Log($"Waiting for {reactivationDelay} seconds before reactivating...");
		yield return new WaitForSeconds(reactivationDelay);
		Debug.Log("Delay finished. Attempting reactivation.");
		reactivationCoroutine = null;
		ActivateDictationService();
	}

	private void ActivateDictationService()
	{
		Debug.Log($"Attempting ActivateDictationService. Is AppVoiceExperience null? {appVoiceExperience == null}. Is Active? {appVoiceExperience?.Active}. Is Activating flag set? {isActivating}");
		if (appVoiceExperience != null && !appVoiceExperience.Active && !isActivating)
		{
			Debug.Log("Activating dictation service...");
			isActivating = true;
			appVoiceExperience.Activate();
			Debug.Log("appVoiceExperience.Activate() called.");
		}
		else if (appVoiceExperience == null) { Debug.LogError("Cannot activate dictation, AppVoiceExperience is not assigned."); }
		else if (appVoiceExperience.Active) { Debug.Log("Dictation service already active."); isActivating = false; }
		else if (isActivating) { Debug.Log("Dictation service already activating."); }
	}

	public void ManualStartDictation() // Retained for manual triggering if needed
	{
		ActivateDictationService();
	}
}
