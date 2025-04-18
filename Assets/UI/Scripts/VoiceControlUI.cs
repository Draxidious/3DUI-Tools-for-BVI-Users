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
/// Finds active buttons (interactable/non-interactable) and text elements, maps buttons by name/text,
/// triggers button onClick events, and describes UI content (including type and inactive status)
/// sorted approximately top-to-bottom, within a scope (default or named canvas parent).
/// Pairs well with VoiceGrabController for handling different types of voice interactions.
/// </summary>
public class VoiceUIController : MonoBehaviour
{
	// Helper struct to hold information about UI elements for sorting
	private struct UIElementInfo
	{
		public RectTransform RectTransform; // For position sorting
		public string ElementType; // "Button", "Text"
		public string NameToSpeak;
		public string Status; // e.g., "inactive"
	}

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
	public bool describeButtonInactiveState = true;

	// Map CLEANED, LOWERCASE names (from text or GameObject) to a LIST of Buttons (Used primarily for interaction matching now)
	private Dictionary<string, List<Button>> interactableButtonsByCleanName = new Dictionary<string, List<Button>>();
	// Store the original text/name used for mapping, useful for screen reader function if needed elsewhere, but description builds dynamically now
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
		// Initial scan still useful for mapping buttons for interaction lookup
		FindAndMapInteractableButtons();
	}

	/// <summary>
	/// Finds UI Buttons within the default scope and maps them for interaction lookup.
	/// Description methods now perform their own searches.
	/// </summary>
	public void FindAndMapInteractableButtons()
	{
		// [Code remains the same as previous version - populates dictionaries]
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

			if (interactableButtonsByCleanName.TryGetValue(cleanedName, out List<Button> buttonList))
			{
				buttonList.Add(button);
			}
			else
			{
				interactableButtonsByCleanName.Add(cleanedName, new List<Button> { button });
			}
			buttonToSpokenNameMap[button] = spokenName;
			mappedCount++;
		}
		Debug.Log($"Initial Mapping: Mapped {mappedCount} Buttons across {interactableButtonsByCleanName.Count} unique cleaned names for interaction.");
	}

	/// <summary>
	/// Helper to get text from TextMeshPro or legacy Text component.
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
	/// Cleans a name for use as a dictionary key.
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
	/// </summary>
	public bool IsUIInteractable(string dictatedInput)
	{
		// [Code remains the same]
		string bestMatch = FindBestMatchingCleanedName(dictatedInput);
		if (bestMatch != null && interactableButtonsByCleanName.TryGetValue(bestMatch, out var candidates))
		{
			return candidates.Any(b => b != null && b.gameObject.activeInHierarchy && b.IsInteractable());
		}
		return false;
	}

	/// <summary>
	/// Attempts to "click" a UI Button based on voice input. Only clicks if interactable.
	/// </summary>
	public void InteractWithUIByName(string dictatedInput)
	{
		Debug.LogWarning("said name: " + dictatedInput);
		// [Code remains the same]
		string targetCleanedName = FindBestMatchingCleanedName(dictatedInput);
		Debug.LogWarning("clean name: "+ targetCleanedName);
		
		if (targetCleanedName == null) { return; }
		if (!interactableButtonsByCleanName.TryGetValue(targetCleanedName, out List<Button> candidates))
		{
			Debug.LogError($"Voice UI Interaction: Internal error. Found key '{targetCleanedName}' but no list.");
			ttsSpeaker.Speak("Sorry, something went wrong."); return;
		}
		Debug.LogWarning("looked for button");
		Button buttonToClick = candidates.FirstOrDefault(b => b != null && b.gameObject.activeInHierarchy && b.IsInteractable());
		if (buttonToClick == null)
		{
			bool anyExistButNotInteractable = candidates.Any(b => b != null && b.gameObject.activeInHierarchy && !b.IsInteractable());
			if (anyExistButNotInteractable) { ttsSpeaker.SpeakQueued($"{targetCleanedName} is not interactable right now."); }
			else { ttsSpeaker.SpeakQueued($"Cannot interact with {targetCleanedName} right now."); }
			return;
		}
		PerformUIClick(buttonToClick);
		ttsSpeaker.SpeakQueued("clicked");
		Debug.LogWarning("clicked");
	}

	/// <summary>
	/// Internal function to handle the actual UI button click and feedback.
	/// </summary>
	private void PerformUIClick(Button button)
	{
		// [Code remains the same]
		if (button == null) return;
		string nameToSpeak = GetSpokenNameForButton(button);
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

	/// <summary>
	/// Helper to get the appropriate spoken name for a button based on settings.
	/// </summary>
	private string GetSpokenNameForButton(Button button)
	{
		// [Code remains the same]
		if (button == null) return "unnamed button";
		if (preferButtonText)
		{
			string buttonText = GetButtonText(button);
			if (!string.IsNullOrWhiteSpace(buttonText))
			{
				return buttonText;
			}
		}
		return CleanNameForDictionary(button.gameObject.name);
	}

	// +++ SCREEN READER FUNCTIONALITY +++

	/// <summary>
	/// Describes active buttons and text elements within the default scope, sorted approximately top-to-bottom.
	/// </summary>
	public void DescribeVisibleUI()
	{
		ProcessAndDescribeScope(null, "visible");
	}

	/// <summary>
	/// Describes active buttons and text elements within a specific Canvas identified by its PARENT's name, sorted approximately top-to-bottom.
	/// </summary>
	public void DescribeCanvasByName(string parentName)
	{
		// [Code remains the same]
		if (!CheckTTSSpeaker()) return;
		if (string.IsNullOrWhiteSpace(parentName))
		{
			Debug.LogError("DescribeCanvasByName: Parent name cannot be empty.");
			SpeakIfAvailable("Please specify which UI area to describe."); return;
		}
		Canvas requestedCanvas = FindObjectsByType<Canvas>(FindObjectsSortMode.None)
			.FirstOrDefault(c => c.transform.parent != null && c.transform.parent.gameObject.name.Equals(parentName, StringComparison.OrdinalIgnoreCase));
		if (requestedCanvas == null)
		{
			Debug.LogWarning($"DescribeCanvasByName: Canvas whose parent is named '{parentName}' not found.");
			SpeakIfAvailable($"Sorry, I couldn't find a UI area named {parentName}."); return;
		}
		ProcessAndDescribeScope(requestedCanvas.gameObject, $"in the {parentName} area");
	}

	/// <summary>
	/// Core logic to find, sort, and describe UI elements within a given scope.
	/// </summary>
	private void ProcessAndDescribeScope(GameObject scope, string context)
	{
		// [Code remains the same - finds elements, adds to list]
		if (!CheckTTSSpeaker()) return;
		List<UIElementInfo> elementsToDescribe = new List<UIElementInfo>();
		HashSet<GameObject> buttonGameObjects = new HashSet<GameObject>();
		GameObject searchRoot = scope;
		if (scope == null && targetCanvas != null) { searchRoot = targetCanvas.gameObject; }

		// Find Buttons
		Button[] buttons;
		if (searchRoot != null) { buttons = searchRoot.GetComponentsInChildren<Button>(true); }
		else { buttons = FindObjectsByType<Button>(FindObjectsSortMode.None); }
		foreach (var button in buttons)
		{
			if (button != null && button.gameObject.activeInHierarchy && button.TryGetComponent<RectTransform>(out var rectTransform))
			{
				string nameToSpeak = GetSpokenNameForButton(button);
				string status = (describeButtonInactiveState && !button.IsInteractable()) ? "inactive" : "";
				elementsToDescribe.Add(new UIElementInfo { RectTransform = rectTransform, ElementType = "Button", NameToSpeak = nameToSpeak, Status = status });
				buttonGameObjects.Add(button.gameObject);
			}
			else if (button != null && button.gameObject.activeInHierarchy)
			{
				Debug.LogWarning($"Button '{button.name}' lacks a RectTransform, cannot sort by position.", button.gameObject);
			}
		}

		// Find Text
		if (describeStandaloneText)
		{
			// Find TextMeshProUGUI
			TextMeshProUGUI[] textComponentsTMP;
			if (searchRoot != null) { textComponentsTMP = searchRoot.GetComponentsInChildren<TextMeshProUGUI>(true); }
			else { textComponentsTMP = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None); }
			foreach (var tmp in textComponentsTMP)
			{
				if (tmp != null && tmp.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(tmp.text) && tmp.TryGetComponent<RectTransform>(out var rectTransform))
				{
					bool ignore = tmp.transform.parent != null && buttonGameObjects.Contains(tmp.transform.parent.gameObject);
					if (!ignore) { elementsToDescribe.Add(new UIElementInfo { RectTransform = rectTransform, ElementType = "Text", NameToSpeak = tmp.text, Status = "" }); }
				}
			}
			// Find legacy Text
			Text[] textComponentsLegacy;
			if (searchRoot != null) { textComponentsLegacy = searchRoot.GetComponentsInChildren<Text>(true); }
			else { textComponentsLegacy = FindObjectsByType<Text>(FindObjectsSortMode.None); }
			foreach (var text in textComponentsLegacy)
			{
				if (text != null && text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(text.text) && text.TryGetComponent<RectTransform>(out var rectTransform))
				{
					bool ignore = text.transform.parent != null && buttonGameObjects.Contains(text.transform.parent.gameObject);
					if (!ignore) { elementsToDescribe.Add(new UIElementInfo { RectTransform = rectTransform, ElementType = "Text", NameToSpeak = text.text, Status = "" }); }
				}
			}
		}

		// Sort Elements
		var sortedElements = elementsToDescribe.OrderByDescending(info => info.RectTransform.position.y).ToList();

		// Build Description String
		StringBuilder description = new StringBuilder();
		for (int i = 0; i < sortedElements.Count; i++)
		{
			AppendElementDescription(description, i, sortedElements[i]); // Call updated helper
		}

		// Speak Result
		SpeakDescriptionResult(description, sortedElements.Count, context);
		Debug.Log($"VoiceUI Description ({context}): Found and sorted {sortedElements.Count} elements.");
	}


	/// <summary>
	/// Helper to append a single element's description to the StringBuilder.
	/// Formats as "[Status] [Type] [Name]" (e.g., "inactive Button Settings", "Button Start", "Welcome Text").
	/// </summary>
	/// <param name="builder">StringBuilder to append to.</param>
	/// <param name="index">Index of the element in the sorted list (for separators).</param>
	/// <param name="elementInfo">Information about the element to describe.</param>
	private void AppendElementDescription(StringBuilder builder, int index, UIElementInfo elementInfo)
	{
		if (index > 0)
		{
			builder.Append(". "); // Separator between elements
		}

		// Determine status prefix (e.g., "inactive ") - includes trailing space if status exists
		string statusPrefix = !string.IsNullOrEmpty(elementInfo.Status) ? elementInfo.Status + " " : "";
		// Determine type prefix (e.g., "Button ") - only if not Text, includes trailing space
		string typePrefix = (elementInfo.ElementType == "Button") ? elementInfo.ElementType + " " : "";
		// Note: Add other types like "Slider ", "Toggle " here if you expand functionality later

		// Sanitize text for speech
		string sanitizedNameToSpeak = elementInfo.NameToSpeak.Replace("\n", " ").Replace("\r", "");

		// Combine in the new order: Status Type Name
		builder.Append($"{statusPrefix}{typePrefix}{sanitizedNameToSpeak}");
	}


	/// <summary>
	/// Helper to speak the final description result.
	/// </summary>
	private void SpeakDescriptionResult(StringBuilder builder, int elementCount, string context)
	{
		// [Code remains the same]
		if (elementCount > 0)
		{
			SpeakIfAvailable(builder.ToString());
		}
		else
		{
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
	/// to update the internal mapping used for interaction lookup. Description is now dynamic.
	/// </summary>
	public void RefreshInteractableButtons()
	{
		// [Code remains the same]
		Debug.Log("VoiceUIController: Refreshing interactable UI Button list mapping for interaction.");
		FindAndMapInteractableButtons();
	}
}
