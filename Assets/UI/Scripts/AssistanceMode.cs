using UnityEngine;
using Oculus.Voice;        // Namespace for AppVoiceExperience
using UnityEngine.Events; // Required for UnityEvent<T>
using System;              // For StringComparison, NonSerialized, TryParse
using System.Collections; // Required for Coroutines
using System.Collections.Generic; // To use List<>
using System.Globalization; // For NumberStyles, CultureInfo
using Meta.WitAi.TTS.Utilities; // Namespace for Meta TTSSpeaker
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

// For commands WITH a single string parameter
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

// For commands with a string AND a number parameter, separated by a word
[System.Serializable]
public class StringNumberWordActionPair
{
	[Tooltip("The specific word/phrase that acts as the command trigger.")]
	public string triggerWord;

	[Tooltip("The word used to separate the string parameter from the number parameter (e.g., 'to', 'set', 'at').")]
	public string separatorWord = "to"; // Default to "to"

	[Tooltip("The UnityEvent(s) to invoke when the pattern '(trigger) (string) (separator) (number)' is detected. Passes the string and integer.")]
	public UnityEvent<string, int> actionToTrigger; // Event with string and int parameters

	// Timestamp for per-command cooldown - Do not save/load this value
	[System.NonSerialized] public float lastExecutionTime = -100f;
}

// For commands with two string parameters, separated by a phrase
[System.Serializable]
public class StringStringWordActionPair
{
	[Tooltip("The specific word/phrase that acts as the command trigger (e.g., 'put', 'set').")]
	public string triggerWord;

	[Tooltip("The word or phrase used to separate the two string parameters (e.g., 'for', 'as', 'in field').")]
	public string separatorPhrase = "for"; // Default to "for"

	[Tooltip("The UnityEvent(s) to invoke when the pattern '(trigger) (string1) (separator) (string2)' is detected. Passes both strings.")]
	public UnityEvent<string, string> actionToTrigger; // Event with two string parameters

	// NEW: Option to reverse the order of parameters passed to the action
	[Tooltip("If true, pass parameters as (string2, string1) instead of (string1, string2). Example: 'put value for field' -> action(field, value)")]
	public bool reverseParameterOrder = false; // Default to false

	// Timestamp for per-command cooldown - Do not save/load this value
	[System.NonSerialized] public float lastExecutionTime = -100f;
}


// --- Main Script ---

// AssistanceMode script modified to handle per-command cooldowns and multiple parameter types
public class AssistanceMode : MonoBehaviour
{
	[Tooltip("Reference to the AppVoiceExperience component in your scene.")]
	[SerializeField] private AppVoiceExperience appVoiceExperience;

	[Space]
	[Header("Command Configuration - Order Matters (Most Specific First)")]

	[Tooltip("Commands expecting two strings separated by a phrase (e.g., 'put value X for field Y'). Processed first.")]
	[SerializeField] private List<StringStringWordActionPair> stringStringWordActions = new List<StringStringWordActionPair>();

	[Tooltip("Commands expecting a string AND a number (e.g., 'set value X to 10', 'set score to five'). Processed second.")]
	[SerializeField] private List<StringNumberWordActionPair> stringNumberWordActions = new List<StringNumberWordActionPair>();

	[Tooltip("Commands that expect a single string parameter (e.g., 'grab item', 'go location'). Processed third.")]
	[SerializeField] private List<ParameterizedWordActionPair> parameterizedWordActions = new List<ParameterizedWordActionPair>();

	[Tooltip("Simple commands that do not expect a parameter (e.g., 'show help', 'status'). Processed last.")]
	[SerializeField] private List<SimpleWordActionPair> simpleWordActions = new List<SimpleWordActionPair>();
	[Header("TTS Settings")]
	public TTSSpeaker ttsSpeaker;

	[Space]
	[Header("Settings")]
	[Tooltip("Make the word matching case-insensitive?")]
	[SerializeField] private bool ignoreCase = true;

	[Tooltip("Delay in seconds before reactivating dictation after it ends.")]
	[SerializeField] private float reactivationDelay = 0.5f;

	[Tooltip("Cooldown time in seconds before the *same* command can be triggered again.")]
	[SerializeField] private float commandCooldownDuration = 5.0f; // Default to 5 seconds

	public bool isListening = true;
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

		// Check if any commands are configured
		if ((parameterizedWordActions == null || parameterizedWordActions.Count == 0) &&
			(simpleWordActions == null || simpleWordActions.Count == 0) &&
			(stringNumberWordActions == null || stringNumberWordActions.Count == 0) &&
			(stringStringWordActions == null || stringStringWordActions.Count == 0)) // Check new list
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
		if (stringNumberWordActions != null)
		{
			foreach (var pair in stringNumberWordActions) pair.lastExecutionTime = initialTime;
		}
		// Initialize new command type cooldowns
		if (stringStringWordActions != null)
		{
			foreach (var pair in stringStringWordActions) pair.lastExecutionTime = initialTime;
		}
	}


	// --- Transcription Handling ---

	private void HandleTranscription(string transcript)
	{
		if (string.IsNullOrEmpty(transcript)) return;

		Debug.Log($"Transcription received: '{transcript}'");
		StringComparison comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
		bool commandRecognizedThisTranscript = false;

		// --- IMPORTANT: Process commands in order of specificity (most specific first) ---

		if (isListening)
		{   // 1. Check for String + String Commands
			if (stringStringWordActions != null && !commandRecognizedThisTranscript)
			{
				foreach (StringStringWordActionPair pair in stringStringWordActions)
				{
					if (string.IsNullOrEmpty(pair.triggerWord) || string.IsNullOrEmpty(pair.separatorPhrase)) continue;

					// Check if transcript starts with the trigger word (whole word)
					if (transcript.StartsWith(pair.triggerWord, comparison))
					{
						int triggerEndIndex = pair.triggerWord.Length;
						// Ensure it's a whole word match at the start (check boundary)
						if (triggerEndIndex >= transcript.Length || !char.IsLetterOrDigit(transcript[triggerEndIndex]))
						{
							string textAfterTrigger = transcript.Substring(triggerEndIndex).TrimStart();

							// Find the separator phrase (case-insensitive), checking whole word boundaries
							int separatorIndex = -1;
							int currentSearchIndex = 0;
							while (currentSearchIndex < textAfterTrigger.Length)
							{
								int potentialIndex = textAfterTrigger.IndexOf(pair.separatorPhrase, currentSearchIndex, comparison);
								if (potentialIndex == -1) break; // Separator not found

								// Check boundaries
								bool startBoundaryOK = (potentialIndex == 0) || !char.IsLetterOrDigit(textAfterTrigger[potentialIndex - 1]);
								int separatorEndIndex = potentialIndex + pair.separatorPhrase.Length;
								bool endBoundaryOK = (separatorEndIndex == textAfterTrigger.Length) || !char.IsLetterOrDigit(textAfterTrigger[separatorEndIndex]);

								if (startBoundaryOK && endBoundaryOK)
								{
									separatorIndex = potentialIndex;
									break; // Found valid separator
								}
								// Separator found but not as whole word, continue searching after this instance
								currentSearchIndex = potentialIndex + 1;
							}


							if (separatorIndex > 0) // Separator found, and there must be text before it
							{
								string stringParam1 = textAfterTrigger.Substring(0, separatorIndex).Trim(); // Text before separator
								string stringParam2 = textAfterTrigger.Substring(separatorIndex + pair.separatorPhrase.Length).Trim(); // Text after separator

								// Ensure both parameters are non-empty after trimming
								if (!string.IsNullOrWhiteSpace(stringParam1) && !string.IsNullOrWhiteSpace(stringParam2))
								{
									// Check per-command cooldown
									if (Time.time >= pair.lastExecutionTime + commandCooldownDuration)
									{
										pair.lastExecutionTime = Time.time; // Update timestamp before invoking

										// *** MODIFIED PART: Check reverseParameterOrder flag ***
										if (pair.reverseParameterOrder)
										{
											// Invoke with reversed order
											Debug.LogWarning($"String/String command '{pair.triggerWord}' DETECTED. Invoking with REVERSED order: param1='{stringParam2}', param2='{stringParam1}'");
											pair.actionToTrigger?.Invoke(stringParam2, stringParam1);
										}
										else
										{
											// Invoke with normal order
											Debug.LogWarning($"String/String command '{pair.triggerWord}' DETECTED. Invoking with normal order: param1='{stringParam1}', param2='{stringParam2}'");
											pair.actionToTrigger?.Invoke(stringParam1, stringParam2);
										}

										commandRecognizedThisTranscript = true;
										break; // Found match, stop checking this type
									}
									else
									{
										Debug.LogWarning($"String/String command '{pair.triggerWord}' skipped due to cooldown. Time remaining: {(pair.lastExecutionTime + commandCooldownDuration) - Time.time:F1}s");
									}
								}
							}
						}
					}
				} // End foreach string/string
			}


			// 2. Check for String + Number Commands (only if no string/string command matched)
			if (stringNumberWordActions != null && !commandRecognizedThisTranscript)
			{
				foreach (StringNumberWordActionPair pair in stringNumberWordActions)
				{
					if (string.IsNullOrEmpty(pair.triggerWord) || string.IsNullOrEmpty(pair.separatorWord)) continue;

					if (transcript.StartsWith(pair.triggerWord, comparison))
					{
						int triggerEndIndex = pair.triggerWord.Length;
						if (triggerEndIndex >= transcript.Length || !char.IsLetterOrDigit(transcript[triggerEndIndex]))
						{
							string remainingTranscript = transcript.Substring(triggerEndIndex).TrimStart();
							int separatorIndex = -1;
							int currentSearchIndex = 0;
							while (currentSearchIndex < remainingTranscript.Length)
							{
								int potentialIndex = remainingTranscript.IndexOf(pair.separatorWord, currentSearchIndex, comparison);
								if (potentialIndex == -1) break;

								bool startBoundaryOK = (potentialIndex == 0) || !char.IsLetterOrDigit(remainingTranscript[potentialIndex - 1]);
								int separatorEndIndex = potentialIndex + pair.separatorWord.Length;
								bool endBoundaryOK = (separatorEndIndex == remainingTranscript.Length) || !char.IsLetterOrDigit(remainingTranscript[separatorEndIndex]);

								if (startBoundaryOK && endBoundaryOK)
								{
									separatorIndex = potentialIndex;
									break;
								}
								currentSearchIndex = potentialIndex + 1;
							}


							if (separatorIndex > 0)
							{
								string stringParam = remainingTranscript.Substring(0, separatorIndex).Trim();
								string potentialNumberParam = remainingTranscript.Substring(separatorIndex + pair.separatorWord.Length).Trim();

								if (!string.IsNullOrWhiteSpace(stringParam) &&
									TryParseNumberWord(potentialNumberParam, out int numberParam))
								{
									if (Time.time >= pair.lastExecutionTime + commandCooldownDuration)
									{
										Debug.LogWarning($"String/Number command '{pair.triggerWord}' DETECTED with string: '{stringParam}', number: {numberParam} (parsed from '{potentialNumberParam}')");
										pair.lastExecutionTime = Time.time;
										pair.actionToTrigger?.Invoke(stringParam, numberParam);
										commandRecognizedThisTranscript = true;
										break;
									}
									else
									{
										Debug.LogWarning($"String/Number command '{pair.triggerWord}' skipped due to cooldown. Time remaining: {(pair.lastExecutionTime + commandCooldownDuration) - Time.time:F1}s");
									}
								}
							}
						}
					}
				} // End foreach string/number
			}


			// 3. Check for Single Parameterized Commands (only if no other command matched)
			if (parameterizedWordActions != null && !commandRecognizedThisTranscript)
			{
				foreach (ParameterizedWordActionPair pair in parameterizedWordActions)
				{
					if (string.IsNullOrEmpty(pair.triggerWord)) continue;

					if (transcript.StartsWith(pair.triggerWord, comparison))
					{
						int triggerEndIndex = pair.triggerWord.Length;
						if (triggerEndIndex >= transcript.Length || !char.IsLetterOrDigit(transcript[triggerEndIndex]))
						{
							string parameter = transcript.Substring(triggerEndIndex).Trim();
							if (!string.IsNullOrWhiteSpace(parameter))
							{
								if (Time.time >= pair.lastExecutionTime + commandCooldownDuration)
								{
									Debug.Log($"Parameterized command '{pair.triggerWord}' DETECTED with parameter: '{parameter}'");
									pair.lastExecutionTime = Time.time;
									pair.actionToTrigger?.Invoke(parameter);
									commandRecognizedThisTranscript = true;
									break;
								}
								else
								{
									Debug.Log($"Parameterized command '{pair.triggerWord}' skipped due to cooldown. Time remaining: {(pair.lastExecutionTime + commandCooldownDuration) - Time.time:F1}s");
								}
							}
						}
					}
				} // End foreach parameterized
			}

			// 4. Check for Simple Commands (only if no other command matched)
			if (simpleWordActions != null && !commandRecognizedThisTranscript)
			{
				foreach (SimpleWordActionPair pair in simpleWordActions)
				{
					if (string.IsNullOrEmpty(pair.triggerWord)) continue;

					if (string.Equals(transcript.Trim(), pair.triggerWord, comparison))
					{
						if (Time.time >= pair.lastExecutionTime + commandCooldownDuration)
						{
							Debug.Log($"Simple command '{pair.triggerWord}' DETECTED.");
							pair.lastExecutionTime = Time.time;
							pair.actionToTrigger?.Invoke();
							commandRecognizedThisTranscript = true;
							break;
						}
						else
						{
							Debug.Log($"Simple command '{pair.triggerWord}' skipped due to cooldown. Time remaining: {(pair.lastExecutionTime + commandCooldownDuration) - Time.time:F1}s");
						}
					}
				} // End foreach simple
			}

		}
		if (!commandRecognizedThisTranscript)
		{
			Debug.Log($"No configured commands recognized or triggered in the transcript: '{transcript}'");
		}
	}

	// --- Helper Function for Number Parsing ---

	/// <summary>
	/// Attempts to parse a string as an integer, accepting both digits ("123")
	/// and common English number words ("one", "two", "ten", "twenty").
	/// </summary>
	/// <param name="input">The string potentially containing a number.</param>
	/// <param name="result">The parsed integer value if successful.</param>
	/// <returns>True if parsing was successful, false otherwise.</returns>
	private bool TryParseNumberWord(string input, out int result)
	{
		result = 0;
		if (string.IsNullOrWhiteSpace(input))
		{
			return false;
		}

		// First, try direct integer parsing (most common case)
		if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
		{
			return true;
		}

		// If direct parsing fails, check for number words (case-insensitive)
		string lowerInput = input.Trim().ToLowerInvariant();

		switch (lowerInput)
		{
			case "zero": result = 0; return true;
			case "one": result = 1; return true;
			case "two": result = 2; return true;
			case "three": result = 3; return true;
			case "four": result = 4; return true;
			case "five": result = 5; return true;
			case "six": result = 6; return true;
			case "seven": result = 7; return true;
			case "eight": result = 8; return true;
			case "nine": result = 9; return true;
			case "ten": result = 10; return true;
			case "eleven": result = 11; return true;
			case "twelve": result = 12; return true;
			case "thirteen": result = 13; return true;
			case "fourteen": result = 14; return true;
			case "fifteen": result = 15; return true;
			case "sixteen": result = 16; return true;
			case "seventeen": result = 17; return true;
			case "eighteen": result = 18; return true;
			case "nineteen": result = 19; return true;
			case "twenty": result = 20; return true;
			// Add more words as needed (e.g., tens, hundreds)
			case "thirty": result = 30; return true;
			case "forty": result = 40; return true;
			case "fifty": result = 50; return true;
			case "sixty": result = 60; return true;
			case "seventy": result = 70; return true;
			case "eighty": result = 80; return true;
			case "ninety": result = 90; return true;
			case "hundred": result = 100; return true; // Basic hundred

			default:
				return false; // Not a recognized digit string or number word
		}
	}


	// --- Example Action Methods ---

	// Example for Single String Parameter
	public void Example_GrabItem(string item)
	{
		if (!this.gameObject.activeSelf) return;
		Debug.LogWarning($"ASSISTANCE MODE: Example_GrabItem TRIGGERED with item: '{item}'");
		// Add logic to grab the specified item...
	}

	// Example for Single String Parameter
	public void Example_ChangeColor(string colorName)
	{
		if (!this.gameObject.activeSelf) return;
		Debug.LogWarning($"ASSISTANCE MODE: Example_ChangeColor TRIGGERED with color: '{colorName}'");
		GameObject cube = GameObject.Find("MyCube"); // Example: Find an object named MyCube
		if (cube != null)
		{
			Renderer rend = cube.GetComponent<Renderer>();
			if (rend != null)
			{
				Color targetColor;
				if (ColorUtility.TryParseHtmlString(colorName.ToLowerInvariant(), out targetColor))
				{
					rend.material.color = targetColor;
				}
				else
				{
					// Fallback for simple names if TryParseHtmlString fails
					switch (colorName.ToLowerInvariant())
					{
						case "red": targetColor = Color.red; break;
						case "blue": targetColor = Color.blue; break;
						case "green": targetColor = Color.green; break;
						case "yellow": targetColor = Color.yellow; break;
						case "white": targetColor = Color.white; break;
						case "black": targetColor = Color.black; break;
						default: Debug.LogWarning($"Color parameter '{colorName}' not recognized."); return; // Exit if color unknown
					}
					rend.material.color = targetColor;
				}
			}
		}
	}

	// Example for No Parameters
	public void Example_ShowHelp()
	{
		if (!this.gameObject.activeSelf) return;
		Debug.LogWarning($"ASSISTANCE MODE: Example_ShowHelp TRIGGERED (no parameters).");
		// Example: Find and toggle a UI panel
		GameObject helpPanel = GameObject.Find("HelpPanel");
		if (helpPanel != null) helpPanel.SetActive(!helpPanel.activeSelf);
	}

	// Example for No Parameters
	public void Example_ReportStatus()
	{
		if (!this.gameObject.activeSelf) return;
		Debug.LogWarning($"ASSISTANCE MODE: Example_ReportStatus TRIGGERED (no parameters).");
		// Add logic to report status...
	}

	// Example for String and Int Parameters
	public void Example_SetObjectValue(string objectName, int value)
	{
		if (!this.gameObject.activeSelf) return;
		Debug.LogWarning($"ASSISTANCE MODE: Example_SetObjectValue TRIGGERED for object '{objectName}' with value: {value}");
		GameObject targetObject = GameObject.Find(objectName);
		if (targetObject != null)
		{
			Debug.Log($"Found object '{objectName}'. Applying value {value}. (Implement specific logic here)");
		}
		else
		{
			Debug.LogWarning($"Could not find object named '{objectName}'");
		}
	}

	// Example for String and String Parameters
	// Note: Parameter names here (value, fieldName) correspond to the DEFAULT order (reverseParameterOrder = false)
	// If reverseParameterOrder = true, the 'fieldName' variable will receive stringParam1 (text before separator)
	// and 'value' will receive stringParam2 (text after separator). Adjust your receiving method accordingly.
	public void Example_FillTextField(string value, string fieldName)
	{
		if (!this.gameObject.activeSelf) return;
		Debug.LogWarning($"ASSISTANCE MODE: Example_FillTextField TRIGGERED. Received value='{value}', fieldName='{fieldName}'");
		// Add logic here, e.g., find a UI InputField by name/label and set its text
		// Example using Unity UI (requires 'using UnityEngine.UI;')
		/*
		UnityEngine.UI.InputField inputField = FindInputFieldByName(fieldName); // Implement this helper
		if (inputField != null)
		{
			inputField.text = value;
			Debug.Log($"Set InputField '{fieldName}' to '{value}'");
		}
		else
		{
			Debug.LogWarning($"Could not find InputField associated with '{fieldName}'");
		}
		*/
	}


	// --- Dictation Activation/Reactivation Logic (Unchanged) ---

	private void HandleDictationEnded()
	{
		if (appVoiceExperience != null && appVoiceExperience.Active)
		{
			Debug.Log("Dictation request ended (was active). Scheduling reactivation...");
		}
		else
		{
			Debug.Log("Dictation request ended (was not active or null). Scheduling reactivation...");
		}

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
		if (!this.enabled || !this.gameObject.activeInHierarchy)
		{
			Debug.Log("AssistanceMode component is disabled or GameObject is inactive. Skipping dictation activation.");
			return;
		}

		if (appVoiceExperience == null)
		{
			Debug.LogError("Cannot activate dictation, AppVoiceExperience is not assigned.");
			return;
		}

		if (appVoiceExperience.Active)
		{
			isActivating = false;
			return;
		}
		if (isActivating)
		{
			return;
		}

		Debug.Log("Activating dictation service...");
		isActivating = true;
		try
		{
			appVoiceExperience.Activate();
			Debug.Log("appVoiceExperience.Activate() called.");
		}
		catch (Exception e)
		{
			Debug.LogError($"Error activating AppVoiceExperience: {e.Message}");
			isActivating = false;
		}
	}

	public void ManualStartDictation()
	{
		Debug.Log("ManualStartDictation called.");
		ActivateDictationService();
	}

	public void stopListening()
	{
		isListening = false;
		ttsSpeaker.SpeakQueued($"Listening Off");
	}
	public void startListening()
	{
		isListening = true;
		ttsSpeaker.SpeakQueued($"Listening On");
	}
}
