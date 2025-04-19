using UnityEngine;
using Oculus.Voice;       // Namespace for AppVoiceExperience
using UnityEngine.Events; // Required for UnityEvent<T>
using System;             // For StringComparison, NonSerialized
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

	// Timestamp for per-command cooldown - Do not save/load this value
	[System.NonSerialized] public float lastExecutionTime = -100f;
}

// For commands WITH parameters
[System.Serializable]
public class ParameterizedWordActionPair
{
	[Tooltip("The specific word/phrase that acts as the command trigger (parameter expected).")]
	public string triggerWord;

	[Tooltip("The UnityEvent(s) to invoke when the trigger word is detected FOLLOWED BY a parameter. The parameter string will be passed.")]
	public UnityEvent<string> actionToTrigger; // Event with string parameter

	// Timestamp for per-command cooldown - Do not save/load this value
	[System.NonSerialized] public float lastExecutionTime = -100f;
}


// --- Main Script ---

// AssistanceMode script modified to handle per-command cooldowns
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
	[SerializeField] private float reactivationDelay = 0.5f;

	[Tooltip("Cooldown time in seconds before the *same* command can be triggered again.")]
	[SerializeField] private float commandCooldownDuration = 5.0f; // Default to 5 seconds


	// --- Private State ---
	private bool isActivating = false;
	private Coroutine reactivationCoroutine = null;

	// --- Unity Methods ---

	void Start()
	{
		if (appVoiceExperience == null)
		{
			Debug.LogError("AppVoiceExperience reference is not set in AssistanceMode script!", this);
			enabled = false;
			return;
		}

		if ((parameterizedWordActions == null || parameterizedWordActions.Count == 0) &&
			(simpleWordActions == null || simpleWordActions.Count == 0))
		{
			Debug.LogWarning("AssistanceMode: No commands have been configured.", this);
		}

		// Initialize cooldown timestamps to ensure commands are ready on start
		InitializeCooldowns();

		// Subscribe to events
		appVoiceExperience.VoiceEvents.OnFullTranscription.AddListener(HandleTranscription);
		appVoiceExperience.VoiceEvents.OnRequestCompleted.AddListener(HandleDictationEnded);

		Debug.Log($"AssistanceMode initialized. Listening for commands with {commandCooldownDuration}s per-command cooldown.");

		ActivateDictationService();
	}

	void OnDestroy()
	{
		if (reactivationCoroutine != null)
		{
			StopCoroutine(reactivationCoroutine);
			reactivationCoroutine = null;
		}

		if (appVoiceExperience != null)
		{
			appVoiceExperience.VoiceEvents.OnFullTranscription.RemoveListener(HandleTranscription);
			appVoiceExperience.VoiceEvents.OnRequestCompleted.RemoveListener(HandleDictationEnded);
			if (appVoiceExperience.Active) appVoiceExperience.Deactivate();
		}
	}

	// --- Cooldown Initialization ---
	void InitializeCooldowns()
	{
		float initialTime = Time.time - commandCooldownDuration - 1f; // Ensure ready immediately
		if (parameterizedWordActions != null)
		{
			foreach (var pair in parameterizedWordActions) pair.lastExecutionTime = initialTime;
		}
		if (simpleWordActions != null)
		{
			foreach (var pair in simpleWordActions) pair.lastExecutionTime = initialTime;
		}
	}


	// --- Transcription Handling ---

	private void HandleTranscription(string transcript)
	{
		if (string.IsNullOrEmpty(transcript)) return;

		Debug.Log($"Transcription received: '{transcript}'");
		StringComparison comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
		bool commandRecognizedThisTranscript = false;

		// 1. Check for Parameterized Commands
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
								// Check per-command cooldown before invoking
								if (Time.time >= pair.lastExecutionTime + commandCooldownDuration)
								{
									Debug.LogWarning($"Parameterized command '{pair.triggerWord}' DETECTED with parameter: '{parameter}'");
									pair.lastExecutionTime = Time.time; // Update timestamp
									pair.actionToTrigger?.Invoke(parameter);
									commandRecognizedThisTranscript = true;
								}
								else
								{
									Debug.LogWarning($"Parameterized command '{pair.triggerWord}' skipped due to cooldown. Time remaining: {(pair.lastExecutionTime + commandCooldownDuration) - Time.time:F1}s");
								}
							}
						}
					}
				}
			} // End foreach parameterized
		}

		// 2. Check for Simple Commands
		if (simpleWordActions != null)
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
						int endOfTriggerIndex = index + pair.triggerWord.Length;
						bool nothingFollows = endOfTriggerIndex >= transcript.Length;
						bool onlyWhitespaceFollows = !nothingFollows && string.IsNullOrWhiteSpace(transcript.Substring(endOfTriggerIndex));

						if (nothingFollows || onlyWhitespaceFollows)
						{
							// Check per-command cooldown before invoking
							if (Time.time >= pair.lastExecutionTime + commandCooldownDuration)
							{
								Debug.Log($"Simple command '{pair.triggerWord}' DETECTED.");
								pair.lastExecutionTime = Time.time; // Update timestamp
								pair.actionToTrigger?.Invoke();
								commandRecognizedThisTranscript = true;
							}
							else
							{
								Debug.Log($"Simple command '{pair.triggerWord}' skipped due to cooldown. Time remaining: {(pair.lastExecutionTime + commandCooldownDuration) - Time.time:F1}s");
							}
						}
					}
				}
			} // End foreach simple
		}


		if (!commandRecognizedThisTranscript)
		{
			Debug.Log($"No configured commands recognized or triggered in the transcript.");
		}
	}

	// --- Example Action Methods (Keep examples for both types) ---

	public void Example_GrabItem(string item)
	{
		if (!this.gameObject.activeSelf) return;
		Debug.LogWarning($"ASSISTANCE MODE: Example_GrabItem TRIGGERED with item: '{item}'");
		// Add logic to grab the specified item...
	}

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
				Color targetColor = Color.white;
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

	public void Example_ShowHelp()
	{
		if (!this.gameObject.activeSelf) return;
		Debug.LogWarning($"ASSISTANCE MODE: Example_ShowHelp TRIGGERED (no parameters).");
		GameObject helpPanel = GameObject.Find("HelpPanel");
		if (helpPanel != null) helpPanel.SetActive(!helpPanel.activeSelf);
	}

	public void Example_ReportStatus()
	{
		if (!this.gameObject.activeSelf) return;
		Debug.LogWarning($"ASSISTANCE MODE: Example_ReportStatus TRIGGERED (no parameters).");
		// Add logic to report status...
	}


	// --- Dictation Activation/Reactivation Logic (Unchanged) ---

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
		//Debug.Log($"Attempting ActivateDictationService. Is AppVoiceExperience null? {appVoiceExperience == null}. Is Active? {appVoiceExperience?.Active}. Is Activating flag set? {isActivating}");
		if (appVoiceExperience != null && !appVoiceExperience.Active && !isActivating)
		{
			//Debug.Log("Activating dictation service...");
			isActivating = true;
			appVoiceExperience.Activate();
			//Debug.Log("appVoiceExperience.Activate() called.");
		}
		else if (appVoiceExperience == null) { Debug.LogError("Cannot activate dictation, AppVoiceExperience is not assigned."); }
		else if (appVoiceExperience.Active) { Debug.Log("Dictation service already active."); isActivating = false; }
		else if (isActivating) { Debug.Log("Dictation service already activating."); }
	}

	public void ManualStartDictation()
	{
		ActivateDictationService();
	}
}
