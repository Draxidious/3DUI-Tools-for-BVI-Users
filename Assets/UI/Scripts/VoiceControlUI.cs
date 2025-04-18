using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI; // Required for UI elements like Button, Text
using Meta.WitAi.TTS.Utilities; // Namespace for Meta TTSSpeaker
using System.Text.RegularExpressions;
using System.Text; // Required for StringBuilder
				   // Optional: If using TextMeshPro for button labels and standalone text
using TMPro; // Make sure to uncomment this if using TextMeshPro

/// <summary>
/// Manages voice interaction with UI Buttons and describes UI elements like a screen reader.
/// Finds interactable/non-interactable buttons and active text elements, maps buttons by name/text,
/// triggers button onClick events, and describes UI content (including type and inactive status)
/// within a scope (default or named canvas parent).
/// Pairs well with VoiceGrabController for handling different types of voice interactions.
/// </summary>
public class VoiceUIController : MonoBehaviour
{
	// Regex to remove Unity's clone numbers like " (1)" or trailing numbers AFTER converting to lowercase
	private static readonly Regex nameCleaningRegex = new Regex(@"(\s*\(\d+\)|\d+)$", RegexOptions.Compiled);

	[Header("TTS Settings")]
	[Tooltip("Reference to the TTSSpeaker component for voice feedback.")]
	public TTSSpeaker ttsSpeaker;

	[Header("UI Settings")]
	[Tooltip("Optional: Default Canvas to search within if no specific canvas is named. If null, DescribeVisibleUI() searches the entire scene.")]
	public Canvas targetCanvas;
	[Tooltip("Prioritize using the Button's visible Text/TextMeshPro component for matching and reading, instead of the GameObject name.")]
	public bool preferButtonText = true;
	[Tooltip("Include standalone, active Text and TextMeshPro elements in UI descriptions.")]
	public bool describeStandaloneText = true;
	[Tooltip("Indicate when buttons are not interactable (e.g., disabled).")]
	public bool describeButtonInactiveState = true; // New setting

	// Map CLEANED, LOWERCASE names (from text or GameObject) to a LIST of Buttons
	private Dictionary<string, List<Button>> interactableButtonsByCleanName = new Dictionary<string, List<Button>>();
	// Store the original text/name used for mapping, useful for screen reader function
	private Dictionary<Button, string> buttonToSpokenNameMap = new Dictionary<Button, string>();


	void Start()
	{
		if (ttsSpeaker == null)
		{
			ttsSpeaker = GetComponent<TTSSpeaker>();
			if (ttsSpeaker == null)
			{
				Debug.LogError("VoiceUIController: TTSSpeaker component has not been assigned and couldn't be found on this GameObject!");
			}
		}
		// Initial scan based on the default targetCanvas or whole scene
		FindAndMapInteractableButtons();
	}

	/// <summary>
	/// Finds interactable UI Buttons within the default scope (targetCanvas or scene) and maps them.
	/// Populates dictionaries used for interaction and the default DescribeVisibleUI().
	/// </summary>
	public void FindAndMapInteractableButtons()
	{
		// [Code for finding and mapping buttons remains largely the same]
		// ... (omitted for brevity, same as previous version) ...
		interactableButtonsByCleanName.Clear();
		buttonToSpokenNameMap.Clear();
		Button[] allButtons;

		if (targetCanvas != null)
		{
			allButtons = targetCanvas.GetComponentsInChildren<Button>(true);
		}
		else
		{
			allButtons = FindObjectsByType<Button>(FindObjectsSortMode.None);
		}

		int mappedCount = 0;
		foreach (var button in allButtons)
		{
			string originalGameObjectName = button.gameObject.name;
			string nameToUse = originalGameObjectName;
			string spokenName = originalGameObjectName;

			if (preferButtonText)
			{
				string foundText = GetButtonText(button);
				if (!string.IsNullOrWhiteSpace(foundText))
				{
					nameToUse = foundText;
					spokenName = foundText;
				}
				else { spokenName = CleanNameForDictionary(originalGameObjectName); }
			}
			else { spokenName = CleanNameForDictionary(originalGameObjectName); }

			string cleanedName = CleanNameForDictionary(nameToUse);

			if (string.IsNullOrEmpty(cleanedName) || cleanedName == "unnamed ui element")
			{
				continue;
			}

			// Map buttons regardless of interactable state for potential description
			if (interactableButtonsByCleanName.TryGetValue(cleanedName, out List<Button> buttonList))
			{
				buttonList.Add(button);
			}
			else
			{
				interactableButtonsByCleanName.Add(cleanedName, new List<Button> { button });
			}
			// Store spoken name for all mapped buttons
			buttonToSpokenNameMap[button] = spokenName;
			mappedCount++;
		}
		Debug.Log($"Initial Mapping: Mapped {mappedCount} Buttons across {interactableButtonsByCleanName.Count} unique cleaned names.");
	}

	/// <summary>
	/// Helper to get text from TextMeshPro or legacy Text component on a button's children.
	/// </summary>
	private string GetButtonText(Button button)
	{
		// [Code remains the same]
		if (button == null) return null;
		TextMeshProUGUI buttonTextTMP = button.GetComponentInChildren<TextMeshProUGUI>();
		if (buttonTextTMP != null && !string.IsNullOrWhiteSpace(buttonTextTMP.text)) { return buttonTextTMP.text; }
		Text buttonTextLegacy = button.GetComponentInChildren<Text>();
		if (buttonTextLegacy != null && !string.IsNullOrWhiteSpace(buttonTextLegacy.text)) { return buttonTextLegacy.text; }
		return null;
	}

	/// <summary>
	/// Cleans a name (from GameObject or Text) for use as a dictionary key.
	/// </summary>
	private string CleanNameForDictionary(string originalName)
	{
		// [Code remains the same]
		if (string.IsNullOrEmpty(originalName)) return string.Empty;
		string lowerName = originalName.ToLowerInvariant();
		string cleaned = nameCleaningRegex.Replace(lowerName, "").Trim();
		return string.IsNullOrEmpty(cleaned) ? "unnamed ui element" : cleaned;
	}

	/// <summary>
	/// Cleans dictated input for matching.
	/// </summary>
	private string CleanDictatedInput(string dictatedInput)
	{
		// [Code remains the same]
		if (string.IsNullOrEmpty(dictatedInput)) return string.Empty;
		return dictatedInput.ToLowerInvariant().Trim();
	}

	/// <summary>
	/// Finds the best matching cleaned button name key based on dictated input.
	/// </summary>
	private string FindBestMatchingCleanedName(string dictatedInput)
	{
		// [Code remains the same]
		string cleanedInput = CleanDictatedInput(dictatedInput);
		if (string.IsNullOrEmpty(cleanedInput)) return null;
		string bestMatch = null;
		int longestMatchLength = 0;
		foreach (string knownCleanedName in interactableButtonsByCleanName.Keys)
		{
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
	/// Checks if the dictated input matches any known *and currently interactable* button.
	/// (Interaction logic still only targets interactable buttons)
	/// </summary>
	public bool IsUIInteractable(string dictatedInput)
	{
		// [Code remains the same - only checks if an INTERACTABLE match exists]
		string bestMatch = FindBestMatchingCleanedName(dictatedInput);
		if (bestMatch != null && interactableButtonsByCleanName.TryGetValue(bestMatch, out var candidates))
		{
			// Check if any candidate matching the name is actually interactable
			return candidates.Any(b => b != null && b.gameObject.activeInHierarchy && b.IsInteractable());
		}
		return false;
	}

	/// <summary>
	/// Attempts to "click" a UI Button based on voice input. Only clicks if interactable.
	/// </summary>
	public void InteractWithUIByName(string dictatedInput)
	{
		// [Code remains the same - only finds and clicks INTERACTABLE buttons]
		string targetCleanedName = FindBestMatchingCleanedName(dictatedInput);
		if (targetCleanedName == null)
		{
			Debug.LogWarning($"Voice UI Interaction: Cannot find mapped UI element matching '{dictatedInput}'.");
			return;
		}
		if (!interactableButtonsByCleanName.TryGetValue(targetCleanedName, out List<Button> candidates))
		{
			Debug.LogError($"Voice UI Interaction: Internal error. Found key '{targetCleanedName}' but no list.");
			SpeakIfAvailable("Sorry, something went wrong.");
			return;
		}
		// IMPORTANT: Still only finds the first INTERACTABLE button to click
		Button buttonToClick = candidates.FirstOrDefault(b => b != null && b.gameObject.activeInHierarchy && b.IsInteractable());
		if (buttonToClick == null)
		{
			// Check if button exists but isn't interactable
			bool anyExistButNotInteractable = candidates.Any(b => b != null && b.gameObject.activeInHierarchy && !b.IsInteractable());
			if (anyExistButNotInteractable)
			{
				SpeakIfAvailable($"{targetCleanedName} is not interactable right now.");
			}
			else
			{
				bool anyExistAtAll = candidates.Any(b => b != null && b.gameObject != null);
				SpeakIfAvailable(anyExistAtAll ? $"{targetCleanedName} is not available right now." : $"Cannot find {targetCleanedName} right now.");
			}
			return;
		}
		if (candidates.Count(b => b != null && b.gameObject.activeInHierarchy && b.IsInteractable()) > 1)
		{
			Debug.LogWarning($"Voice UI Interaction: Multiple interactable buttons match '{targetCleanedName}'. Clicking first: '{buttonToClick.gameObject.name}'.");
		}
		PerformUIClick(buttonToClick);
	}

	/// <summary>
	/// Internal function to handle the actual UI button click and feedback.
	/// </summary>
	private void PerformUIClick(Button button)
	{
		// [Code remains the same]
		if (button == null) return;
		string nameToSpeak;
		if (!buttonToSpokenNameMap.TryGetValue(button, out nameToSpeak))
		{
			string buttonText = GetButtonText(button);
			if (preferButtonText && !string.IsNullOrWhiteSpace(buttonText)) { nameToSpeak = buttonText; }
			else { nameToSpeak = CleanNameForDictionary(button.gameObject.name); }
		}
		Debug.Log($"Voice UI: Invoking onClick for button '{button.gameObject.name}' (Speaking as: '{nameToSpeak}')");
		try
		{
			button.onClick.Invoke();
			SpeakIfAvailable($"Clicked {nameToSpeak}.");
		}
		catch (Exception ex)
		{
			Debug.LogError($"Voice UI: Error invoking onClick for button '{button.gameObject.name}': {ex.Message}", button.gameObject);
			SpeakIfAvailable($"Sorry, there was an error interacting with {nameToSpeak}.");
		}
	}

	// +++ SCREEN READER FUNCTIONALITY +++

	/// <summary>
	/// Describes active buttons (interactable and non-interactable) AND standalone text elements within the default scope.
	/// </summary>
	public void DescribeVisibleUI()
	{
		if (!CheckTTSSpeaker()) return;

		StringBuilder description = new StringBuilder();
		int buttonCount = 0;
		int textCount = 0;
		GameObject scope = (targetCanvas != null) ? targetCanvas.gameObject : null;

		// --- Describe Buttons (including inactive) ---
		// Iterate through all mapped buttons, check active state, then determine interactable status
		HashSet<GameObject> buttonGameObjects = new HashSet<GameObject>();
		foreach (var kvp in buttonToSpokenNameMap)
		{
			Button button = kvp.Key;
			if (button != null && button.gameObject.activeInHierarchy) // Check if button's GO is active
			{
				// Determine status only if describing state is enabled
				string status = "";
				if (describeButtonInactiveState && !button.IsInteractable())
				{
					status = "inactive"; // Or "disabled"
				}
				AppendElementDescription(description, ref buttonCount, "Button", kvp.Value, status);
				buttonGameObjects.Add(button.gameObject);
			}
		}

		// --- Describe Standalone Text (if enabled) ---
		if (describeStandaloneText)
		{
			// Pass button GOs to avoid describing their text labels separately if possible
			DescribeTextElements(description, ref textCount, scope, buttonGameObjects);
		}

		// --- Speak Result ---
		SpeakDescriptionResult(description, buttonCount, textCount, "visible");
		Debug.Log($"VoiceUI DescribeVisibleUI: Found {buttonCount} active buttons and {textCount} text elements in default scope.");
	}

	/// <summary>
	/// Describes active buttons (interactable and non-interactable) AND standalone text elements within a specific Canvas identified by its PARENT's name.
	/// </summary>
	/// <param name="parentName">The name of the PARENT GameObject whose child Canvas should be described.</param>
	public void DescribeCanvasByName(string parentName)
	{
		if (!CheckTTSSpeaker()) return;
		if (string.IsNullOrWhiteSpace(parentName))
		{
			Debug.LogError("DescribeCanvasByName: Parent name cannot be empty.");
			SpeakIfAvailable("Please specify which UI area to describe.");
			return;
		}

		// Find the canvas via parent name
		Canvas requestedCanvas = FindObjectsByType<Canvas>(FindObjectsSortMode.None)
			.FirstOrDefault(c => c.transform.parent != null && c.transform.parent.gameObject.name.Equals(parentName, StringComparison.OrdinalIgnoreCase));

		if (requestedCanvas == null)
		{
			Debug.LogWarning($"DescribeCanvasByName: Canvas whose parent is named '{parentName}' not found.");
			SpeakIfAvailable($"Sorry, I couldn't find a UI area named {parentName}.");
			return;
		}

		// Proceed to describe the contents of this specific canvas
		string actualCanvasName = requestedCanvas.gameObject.name;
		Debug.Log($"DescribeCanvasByName: Found canvas '{actualCanvasName}' under parent '{parentName}'. Describing its content.");

		StringBuilder description = new StringBuilder();
		int buttonCount = 0;
		int textCount = 0;
		GameObject scope = requestedCanvas.gameObject; // Scope is the canvas itself

		// --- Describe Buttons within this canvas (including inactive) ---
		Button[] buttonsInCanvas = scope.GetComponentsInChildren<Button>(true); // Get all buttons
		HashSet<GameObject> buttonGameObjects = new HashSet<GameObject>(); // To track button GOs

		foreach (var button in buttonsInCanvas)
		{
			// Check only if the button's GO is active in the hierarchy
			if (button != null && button.gameObject.activeInHierarchy)
			{
				string nameToSpeak;
				string buttonText = GetButtonText(button);
				if (preferButtonText && !string.IsNullOrWhiteSpace(buttonText))
				{
					nameToSpeak = buttonText;
				}
				else
				{
					nameToSpeak = CleanNameForDictionary(button.gameObject.name);
				}

				// Determine status only if describing state is enabled
				string status = "";
				if (describeButtonInactiveState && !button.IsInteractable())
				{
					status = "inactive";
				}

				AppendElementDescription(description, ref buttonCount, "Button", nameToSpeak, status);
				buttonGameObjects.Add(button.gameObject); // Track the button's GameObject
			}
		}

		// --- Describe Standalone Text within this canvas (if enabled) ---
		if (describeStandaloneText)
		{
			// Pass button GOs to avoid describing their text labels separately if possible
			DescribeTextElements(description, ref textCount, scope, buttonGameObjects);
		}

		// --- Speak Result ---
		SpeakDescriptionResult(description, buttonCount, textCount, $"in the {parentName} area");
		Debug.Log($"VoiceUI DescribeCanvasByName: Found {buttonCount} active buttons and {textCount} text elements in canvas '{actualCanvasName}' under parent '{parentName}'.");
	}

	/// <summary>
	/// Helper method to find and append descriptions for active Text and TextMeshProUGUI elements within a given scope.
	/// </summary>
	private void DescribeTextElements(StringBuilder builder, ref int textCount, GameObject scope, HashSet<GameObject> ignoreIfChildOf = null)
	{
		// Find TextMeshProUGUI elements
		TextMeshProUGUI[] textComponentsTMP;
		if (scope != null) { textComponentsTMP = scope.GetComponentsInChildren<TextMeshProUGUI>(true); }
		else { textComponentsTMP = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None); }

		foreach (var tmp in textComponentsTMP)
		{
			if (tmp != null && tmp.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(tmp.text))
			{
				// Simple check: ignore if the text component's direct parent is in the ignore set
				// This helps avoid reading button labels as separate text if preferButtonText is false
				bool ignore = ignoreIfChildOf != null && tmp.transform.parent != null && ignoreIfChildOf.Contains(tmp.transform.parent.gameObject);

				if (!ignore)
				{
					// Pass empty status for text elements
					AppendElementDescription(builder, ref textCount, "Text", tmp.text, "");
				}
			}
		}

		// Find legacy Text elements
		Text[] textComponentsLegacy;
		if (scope != null) { textComponentsLegacy = scope.GetComponentsInChildren<Text>(true); }
		else { textComponentsLegacy = FindObjectsByType<Text>(FindObjectsSortMode.None); }

		foreach (var text in textComponentsLegacy)
		{
			if (text != null && text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(text.text))
			{
				bool ignore = ignoreIfChildOf != null && text.transform.parent != null && ignoreIfChildOf.Contains(text.transform.parent.gameObject);

				if (!ignore)
				{
					// Pass empty status for text elements
					AppendElementDescription(builder, ref textCount, "Text", text.text, "");
				}
			}
		}
	}


	/// <summary>
	/// Helper to append element description (Button or Text) to the StringBuilder, including type prefix and status suffix.
	/// </summary>
	/// <param name="builder">StringBuilder to append to.</param>
	/// <param name="count">Reference to the count of elements of this type found.</param>
	/// <param name="elementType">Type of the element (e.g., "Button", "Text").</param>
	/// <param name="nameToSpeak">The text/name content of the element.</param>
	/// <param name="status">Optional status text (e.g., "inactive").</param>
	private void AppendElementDescription(StringBuilder builder, ref int count, string elementType, string nameToSpeak, string status = "")
	{
		if (count == 0)
		{
			// First element of this type
			if (builder.Length > 0) builder.Append(" "); // Add space if previous type was already added
														 // Add introductory phrase like "Button elements are: " or "Text elements are: "
			builder.Append($"{elementType} elements are: ");
		}
		else
		{
			builder.Append(". "); // Separator between elements of the same type
		}

		// Prepend type only if it's not "Text" (or add other types here later)
		string prefix = (elementType == "Button") ? elementType + " " : "";
		// Append status if provided
		string suffix = !string.IsNullOrEmpty(status) ? ", " + status : "";

		builder.Append($"{prefix}{nameToSpeak}{suffix}");
		count++;
	}

	/// <summary>
	/// Helper to speak the final description result, potentially combining buttons and text.
	/// </summary>
	private void SpeakDescriptionResult(StringBuilder builder, int buttonCount, int textCount, string context)
	{
		// [Code remains the same - handles combined output]
		if (buttonCount > 0 || textCount > 0)
		{
			// Contains buttons and/or text - builder already has the sections
			builder.Append("."); // End sentence
			SpeakIfAvailable(builder.ToString());
		}
		// Removed separate checks for buttonCount > 0 && textCount > 0 etc. as the builder handles it.
		// else if (buttonCount > 0) { ... }
		// else if (textCount > 0) { ... }
		else
		{
			// Neither found
			SpeakIfAvailable($"There are no interactable buttons or text elements {context} right now.");
		}
	}

	// --- End Screen Reader Functionality ---


	/// <summary>
	/// Helper method to queue text for speaking via the TTSSpeaker.
	/// </summary>
	private void SpeakIfAvailable(string textToSpeak)
	{
		// [Code remains the same]
		if (ttsSpeaker != null && !string.IsNullOrEmpty(textToSpeak))
		{
			ttsSpeaker.SpeakQueued(textToSpeak);
		}
		else if (ttsSpeaker == null)
		{
			Debug.LogError("VoiceUIController: TTSSpeaker not assigned. Cannot speak.");
		}
	}

	/// <summary>
	/// Helper to check if TTSSpeaker is assigned.
	/// </summary>
	private bool CheckTTSSpeaker()
	{
		// [Code remains the same]
		if (ttsSpeaker != null) return true;
		Debug.LogError("VoiceUIController: TTSSpeaker is not assigned. Cannot perform TTS operations.");
		return false;
	}

	/*
    // Optional: Example coroutine for visual feedback
    private System.Collections.IEnumerator HighlightButtonFeedback(Button button) { ... }
    */

	/// <summary>
	/// Call this method if UI Buttons are added/removed/renamed/text changes dynamically
	/// to update the internal mapping used for general interaction and DescribeVisibleUI().
	/// </summary>
	public void RefreshInteractableButtons()
	{
		// [Code remains the same]
		Debug.Log("VoiceUIController: Refreshing interactable UI Button list and spoken names for default scope.");
		FindAndMapInteractableButtons();
	}
}
