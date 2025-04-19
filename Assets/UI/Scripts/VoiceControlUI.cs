using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization; // Needed for NumberStyles and CultureInfo
using System.Linq;
using UnityEngine;
using UnityEngine.UI; // Required for Button, Text, Slider, Toggle, ToggleGroup, LayoutGroup etc.
using Meta.WitAi.TTS.Utilities;
using System.Text.RegularExpressions;
using System.Text;
using TMPro; // Required for TextMeshProUGUI, TMP_InputField, TMP_Dropdown

/// <summary>
/// Manages voice interaction with UI elements and describes UI like a screen reader.
/// Finds various active UI elements, maps interactable ones by name/text (using heuristics for sliders),
/// triggers actions (clicks, toggles, slider sets), and describes UI content sequentially based on hierarchy/layout groups.
/// String comparisons ignore case, internal whitespace, and punctuation. Attempts internal detection
/// of UI changes after button clicks for basic context feedback and automatically refreshes the
/// interactable element dictionaries after clicks.
/// </summary>
public class VoiceUIController : MonoBehaviour
{
	// Helper struct to hold information about UI elements
	private struct UIElementInfo
	{
		public RectTransform RectTransform;
		public float Distance;
		public string ElementType;
		public string NameToSpeak;
		public string Status;
	}

	// Regex definitions...
	private static readonly Regex nameCleaningRegex = new Regex(@"(\s*\(\d+\)|\d+)$", RegexOptions.Compiled);
	private static readonly Regex whitespaceRemovalRegex = new Regex(@"\s+", RegexOptions.Compiled);
	private static readonly Regex punctuationRemovalRegex = new Regex(@"[\p{P}]", RegexOptions.Compiled);
	private static readonly Regex setSliderRegex = new Regex(@"set (.+)\s+(to|set to)\s+([\d\.\-]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);


	[Header("TTS Settings")]
	[Tooltip("Reference to the TTSSpeaker component for voice feedback.")]
	public TTSSpeaker ttsSpeaker;

	[Header("UI Settings")]
	[Tooltip("Optional: Default Canvas to search within for the initial element map and dictionary refreshes. If null, scans the entire scene.")]
	public Canvas targetCanvas;
	[Tooltip("Prioritize using the element's visible Text/TextMeshPro component for matching and reading, instead of the GameObject name.")]
	public bool preferElementText = true;
	[Tooltip("Include standalone, active Text and TextMeshPro elements in UI descriptions.")]
	public bool describeStandaloneText = true;
	[Tooltip("Indicate when interactable elements (Buttons, Toggles, etc.) are not interactable.")]
	public bool describeInactiveState = true;
	[Tooltip("Delay in seconds after a click before checking for UI changes and refreshing dictionaries.")]
	public float postClickCheckDelay = 0.02f;

	// Dictionaries for MAPPING interactable elements by cleaned name
	private Dictionary<string, List<Button>> interactableButtonsByCleanName = new Dictionary<string, List<Button>>();
	private Dictionary<string, List<Toggle>> interactableTogglesByCleanName = new Dictionary<string, List<Toggle>>();
	private Dictionary<string, List<Slider>> interactableSlidersByCleanName = new Dictionary<string, List<Slider>>();
	// Dictionary for storing original SPOKEN names
	private Dictionary<Component, string> elementToSpokenNameMap = new Dictionary<Component, string>();

	// Coroutine reference
	private Coroutine postClickCoroutine = null;


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
		MapAllInteractableElements(); // Map all supported interactable elements at start
	}

	/// <summary>
	/// Finds and maps all supported interactable UI elements (Buttons, Toggles, Sliders).
	/// Uses heuristics to find labels for sliders. Maps sliders using both base name and "name + slider".
	/// </summary>
	public void MapAllInteractableElements()
	{
		// Clear existing maps
		interactableButtonsByCleanName.Clear();
		interactableTogglesByCleanName.Clear();
		interactableSlidersByCleanName.Clear();
		elementToSpokenNameMap.Clear();

		string scopeMsg = (targetCanvas == null) ? "entire scene" : $"Canvas '{targetCanvas.name}'";
		Debug.Log($"MapAllInteractableElements: Scanning {scopeMsg}...");

		int buttonMappedCount = 0;
		int toggleMappedCount = 0;
		int sliderMappedCount = 0;

		// --- Map Buttons ---
		Button[] allButtons;
		if (targetCanvas != null)
		{
			allButtons = targetCanvas.GetComponentsInChildren<Button>(true);
		}
		else
		{
			allButtons = FindObjectsByType<Button>(FindObjectsSortMode.None);
		}
		foreach (var button in allButtons)
		{
			string originalName = button.gameObject.name;
			string nameToUse = originalName;
			string spokenName = originalName;
			if (preferElementText)
			{
				string foundText = GetButtonText(button);
				if (!string.IsNullOrWhiteSpace(foundText))
				{
					nameToUse = foundText;
					spokenName = foundText;
				}
				else
				{
					spokenName = originalName;
				}
			}
			else
			{
				spokenName = originalName;
			}
			string cleanedName = CleanNameForDictionary(nameToUse);
			if (string.IsNullOrEmpty(cleanedName) || cleanedName == "unnameduielement")
			{
				continue;
			}
			if (!interactableButtonsByCleanName.TryGetValue(cleanedName, out List<Button> buttonList))
			{
				buttonList = new List<Button>();
				interactableButtonsByCleanName.Add(cleanedName, buttonList);
			}
			if (!buttonList.Contains(button))
			{
				buttonList.Add(button);
			}
			elementToSpokenNameMap[button] = spokenName;
			buttonMappedCount++;
		}

		// --- Map Toggles ---
		Toggle[] allToggles;
		if (targetCanvas != null)
		{
			allToggles = targetCanvas.GetComponentsInChildren<Toggle>(true);
		}
		else
		{
			allToggles = FindObjectsByType<Toggle>(FindObjectsSortMode.None);
		}
		foreach (var toggle in allToggles)
		{
			string originalName = toggle.gameObject.name;
			string nameToUse = originalName;
			string spokenName = originalName;
			string labelText = GetToggleLabelText(toggle);
			if (preferElementText && !string.IsNullOrWhiteSpace(labelText))
			{
				nameToUse = labelText;
				spokenName = labelText;
			}
			else
			{
				spokenName = originalName;
			}
			string cleanedName = CleanNameForDictionary(nameToUse);
			if (string.IsNullOrEmpty(cleanedName) || cleanedName == "unnameduielement")
			{
				continue;
			}
			if (!interactableTogglesByCleanName.TryGetValue(cleanedName, out List<Toggle> toggleList))
			{
				toggleList = new List<Toggle>();
				interactableTogglesByCleanName.Add(cleanedName, toggleList);
			}
			if (!toggleList.Contains(toggle))
			{
				toggleList.Add(toggle);
			}
			elementToSpokenNameMap[toggle] = spokenName;
			toggleMappedCount++;
		}

		// --- Map Sliders ---
		Slider[] allSliders;
		if (targetCanvas != null)
		{
			allSliders = targetCanvas.GetComponentsInChildren<Slider>(true);
		}
		else
		{
			allSliders = FindObjectsByType<Slider>(FindObjectsSortMode.None);
		}
		foreach (var slider in allSliders)
		{
			string originalName = slider.gameObject.name;
			string baseName = originalName;
			string spokenName = originalName;
			string labelText = GetSliderLabelText(slider);
			if (preferElementText && !string.IsNullOrWhiteSpace(labelText))
			{
				baseName = labelText;
				spokenName = labelText;
			}
			else
			{
				spokenName = originalName;
			}
			string cleanedBaseName = CleanNameForDictionary(baseName);
			string cleanedFullName = CleanNameForDictionary(baseName + " slider");
			if (!string.IsNullOrEmpty(cleanedBaseName) && cleanedBaseName != "unnameduielement")
			{
				if (!interactableSlidersByCleanName.TryGetValue(cleanedBaseName, out List<Slider> sliderList))
				{
					sliderList = new List<Slider>();
					interactableSlidersByCleanName.Add(cleanedBaseName, sliderList);
				}
				if (!sliderList.Contains(slider))
				{
					sliderList.Add(slider);
				}
			}
			if (!string.IsNullOrEmpty(cleanedFullName) && cleanedFullName != "unnameduielement" && cleanedFullName != cleanedBaseName)
			{
				if (!interactableSlidersByCleanName.TryGetValue(cleanedFullName, out List<Slider> sliderList))
				{
					sliderList = new List<Slider>();
					interactableSlidersByCleanName.Add(cleanedFullName, sliderList);
				}
				if (!sliderList.Contains(slider))
				{
					sliderList.Add(slider);
				}
			}
			elementToSpokenNameMap[slider] = spokenName;
			sliderMappedCount++;
		}

		Debug.Log($"MapAllInteractableElements: Mapped {buttonMappedCount} Btns ({interactableButtonsByCleanName.Count} names), " +
				  $"{toggleMappedCount} Tgls ({interactableTogglesByCleanName.Count} names), " +
				  $"{sliderMappedCount} Sliders ({interactableSlidersByCleanName.Count} names) from {scopeMsg}.");
	}

	/// <summary>
	/// Helper to get text from a Button's child Text/TextMeshProUGUI component.
	/// </summary>
	private string GetButtonText(Button button)
	{
		if (button == null)
		{
			return null;
		}
		TextMeshProUGUI buttonTextTMP = button.GetComponentInChildren<TextMeshProUGUI>();
		if (buttonTextTMP != null && !string.IsNullOrWhiteSpace(buttonTextTMP.text))
		{
			return buttonTextTMP.text;
		}
		Text buttonTextLegacy = button.GetComponentInChildren<Text>();
		if (buttonTextLegacy != null && !string.IsNullOrWhiteSpace(buttonTextLegacy.text))
		{
			return buttonTextLegacy.text;
		}
		return null;
	}

	/// <summary>
	/// Helper to get text from a Toggle's child or sibling Text/TextMeshProUGUI component.
	/// </summary>
	private string GetToggleLabelText(Toggle toggle)
	{
		if (toggle == null)
		{
			return null;
		}
		TextMeshProUGUI labelTMP = toggle.GetComponentInChildren<TextMeshProUGUI>();
		if (labelTMP != null && !string.IsNullOrWhiteSpace(labelTMP.text))
		{
			return labelTMP.text;
		}
		Text labelLegacy = toggle.GetComponentInChildren<Text>();
		if (labelLegacy != null && !string.IsNullOrWhiteSpace(labelLegacy.text))
		{
			return labelLegacy.text;
		}
		Transform parent = toggle.transform.parent;
		if (parent != null)
		{
			foreach (Transform sibling in parent)
			{
				if (sibling == toggle.transform)
				{
					continue;
				}
				TextMeshProUGUI siblingTMP = sibling.GetComponent<TextMeshProUGUI>();
				if (siblingTMP != null && !string.IsNullOrWhiteSpace(siblingTMP.text))
				{
					return siblingTMP.text;
				}
				Text siblingLegacy = sibling.GetComponent<Text>();
				if (siblingLegacy != null && !string.IsNullOrWhiteSpace(siblingLegacy.text))
				{
					return siblingLegacy.text;
				}
			}
		}
		return null;
	}

	/// <summary>
	/// Helper to find a label for a Slider by checking preceding sibling.
	/// </summary>
	private string GetSliderLabelText(Slider slider)
	{
		if (slider == null || slider.transform.parent == null)
		{
			return null;
		}
		Transform parent = slider.transform.parent;
		int sliderIndex = slider.transform.GetSiblingIndex();
		if (sliderIndex > 0)
		{
			Transform potentialLabelSibling = parent.GetChild(sliderIndex - 1);
			if (potentialLabelSibling != null && potentialLabelSibling.gameObject.activeInHierarchy)
			{
				TextMeshProUGUI tmpLabel = potentialLabelSibling.GetComponent<TextMeshProUGUI>();
				if (tmpLabel != null && !string.IsNullOrWhiteSpace(tmpLabel.text))
				{
					return tmpLabel.text;
				}
				Text legacyLabel = potentialLabelSibling.GetComponent<Text>();
				if (legacyLabel != null && !string.IsNullOrWhiteSpace(legacyLabel.text))
				{
					return legacyLabel.text;
				}
			}
		}
		return null;
	}

	/// <summary>
	/// Cleans a name (from GameObject or Text) for use as a dictionary key.
	/// </summary>
	private string CleanNameForDictionary(string originalName)
	{
		if (string.IsNullOrEmpty(originalName))
		{
			return string.Empty;
		}
		string lowerName = originalName.ToLowerInvariant();
		string cleanedOfClones = nameCleaningRegex.Replace(lowerName, "");
		string noPunctuation = punctuationRemovalRegex.Replace(cleanedOfClones, "");
		string noWhitespace = whitespaceRemovalRegex.Replace(noPunctuation, "");
		string finalCleaned = noWhitespace.Trim();
		return string.IsNullOrEmpty(finalCleaned) ? "unnameduielement" : finalCleaned;
	}

	/// <summary>
	/// Cleans dictated input for matching.
	/// </summary>
	private string CleanDictatedInput(string dictatedInput)
	{
		if (string.IsNullOrEmpty(dictatedInput))
		{
			return string.Empty;
		}
		string lower = dictatedInput.ToLowerInvariant();
		string noPunctuation = punctuationRemovalRegex.Replace(lower, "");
		string noWhitespace = whitespaceRemovalRegex.Replace(noPunctuation, "");
		return noWhitespace.Trim();
	}

	// --- Interaction Logic ---

	/// <summary>
	/// Finds the best matching cleaned button name key based on dictated input.
	/// </summary>
	private string FindBestMatchingCleanedName(string dictatedInput, Dictionary<string, List<Button>> buttonDict)
	{
		string cleanedInput = CleanDictatedInput(dictatedInput);
		if (string.IsNullOrEmpty(cleanedInput))
		{
			return null;
		}
		string bestMatch = null;
		int longestMatchLength = 0;
		foreach (string knownCleanedName in buttonDict.Keys)
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
	/// Finds the best matching cleaned toggle name key based on dictated input.
	/// </summary>
	private string FindBestMatchingToggleName(string dictatedInput)
	{
		string cleanedInput = CleanDictatedInput(dictatedInput);
		if (string.IsNullOrEmpty(cleanedInput))
		{
			return null;
		}
		string bestMatch = null;
		int longestMatchLength = 0;
		foreach (string knownCleanedName in interactableTogglesByCleanName.Keys)
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
	/// Finds the best matching cleaned slider name key based on dictated input.
	/// </summary>
	private string FindBestMatchingSliderName(string dictatedInput)
	{
		string cleanedInput = CleanDictatedInput(dictatedInput);
		if (string.IsNullOrEmpty(cleanedInput))
		{
			return null;
		}
		string bestMatch = null;
		int longestMatchLength = 0;
		foreach (string knownCleanedName in interactableSlidersByCleanName.Keys)
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
	public bool IsUIButtonInteractable(string dictatedInput)
	{
		string bestMatch = FindBestMatchingCleanedName(dictatedInput, interactableButtonsByCleanName);
		if (bestMatch != null && interactableButtonsByCleanName.TryGetValue(bestMatch, out var candidates))
		{
			return candidates.Any(b => b != null && b.gameObject.activeInHierarchy && b.IsInteractable());
		}
		return false;
	}

	/// <summary>
	/// Checks if the dictated input matches any known *and currently interactable* toggle.
	/// </summary>
	public bool IsUIToggleInteractable(string dictatedInput)
	{
		string bestMatch = FindBestMatchingToggleName(dictatedInput);
		if (bestMatch != null && interactableTogglesByCleanName.TryGetValue(bestMatch, out var candidates))
		{
			return candidates.Any(t => t != null && t.gameObject.activeInHierarchy && t.IsInteractable());
		}
		return false;
	}

	/// <summary>
	/// Checks if the dictated input matches any known *and currently interactable* slider.
	/// </summary>
	public bool IsUISliderInteractable(string dictatedInput)
	{
		string bestMatch = FindBestMatchingSliderName(dictatedInput);
		if (bestMatch != null && interactableSlidersByCleanName.TryGetValue(bestMatch, out var candidates))
		{
			return candidates.Any(s => s != null && s.gameObject.activeInHierarchy && s.IsInteractable());
		}
		return false;
	}

	/// <summary>
	/// Attempts to "click" a UI Button based on voice input.
	/// </summary>
	public void InteractWithUIButtonByName(string dictatedInput)
	{
		string targetCleanedName = FindBestMatchingCleanedName(dictatedInput, interactableButtonsByCleanName);
		if (targetCleanedName == null)
		{
			SpeakIfAvailable($"Could not find button matching {dictatedInput}.");
			return;
		}
		if (!interactableButtonsByCleanName.TryGetValue(targetCleanedName, out List<Button> candidates))
		{
			Debug.LogError($"Voice UI Interaction: Internal error. Key '{targetCleanedName}'.");
			SpeakIfAvailable("Sorry, something went wrong.");
			return;
		}
		Button buttonToClick = candidates.FirstOrDefault(b => b != null && b.gameObject.activeInHierarchy && b.IsInteractable());
		if (buttonToClick == null)
		{
			bool anyExistButNotInteractable = candidates.Any(b => b != null && b.gameObject.activeInHierarchy && !b.IsInteractable());
			string feedbackName = GetSpokenName(candidates.FirstOrDefault()?.GetComponent<Component>(), targetCleanedName);
			if (anyExistButNotInteractable)
			{
				SpeakIfAvailable($"{feedbackName} button is not interactable right now.");
			}
			else
			{
				SpeakIfAvailable($"Cannot interact with {feedbackName} button right now.");
			}
			return;
		}
		PerformClickAndCheckChanges(buttonToClick);
	}

	/// <summary>
	/// Checks a checkbox/toggle based on voice input.
	/// </summary>
	public void CheckCheckboxByName(string dictatedName)
	{
		SetToggleState(dictatedName, true);
	}

	/// <summary>
	/// Unchecks a checkbox/toggle based on voice input.
	/// </summary>
	public void UncheckCheckboxByName(string dictatedName)
	{
		SetToggleState(dictatedName, false);
	}

	/// <summary>
	/// Selects (sets to true) a toggle within a ToggleGroup based on voice input.
	/// </summary>
	public void SelectToggleInGroupByName(string dictatedName)
	{
		SetToggleState(dictatedName, true);
	}

	/// <summary>
	/// Sets a toggle's state. Handles finding, checking interactability, checking current state, and feedback.
	/// </summary>
	private void SetToggleState(string dictatedName, bool desiredState)
	{
		string targetCleanedName = FindBestMatchingToggleName(dictatedName);
		if (targetCleanedName == null)
		{
			SpeakIfAvailable($"Could not find toggle or checkbox matching {dictatedName}.");
			return;
		}
		if (!interactableTogglesByCleanName.TryGetValue(targetCleanedName, out List<Toggle> candidates))
		{
			Debug.LogError($"Voice UI Interaction: Internal error. Key '{targetCleanedName}'.");
			SpeakIfAvailable("Sorry, something went wrong.");
			return;
		}
		Toggle toggleToChange = candidates.FirstOrDefault(t => t != null && t.gameObject.activeInHierarchy && t.IsInteractable());
		if (toggleToChange == null)
		{
			bool anyExistButNotInteractable = candidates.Any(t => t != null && t.gameObject.activeInHierarchy && !t.IsInteractable());
			string feedbackName = GetSpokenName(candidates.FirstOrDefault()?.GetComponent<Component>(), targetCleanedName);
			if (anyExistButNotInteractable)
			{
				SpeakIfAvailable($"{feedbackName} is not interactable right now.");
			}
			else
			{
				SpeakIfAvailable($"Cannot interact with {feedbackName} right now.");
			}
			return;
		}
		bool finalState = desiredState;
		if (toggleToChange.isOn == finalState)
		{
			string currentState = finalState ? "already checked" : "already unchecked";
			if (toggleToChange.group != null && finalState)
			{
				currentState = "already selected";
			}
			string feedbackName = GetSpokenName(toggleToChange, targetCleanedName);
			SpeakIfAvailable($"{feedbackName} is {currentState}.");
			return;
		}
		string actionText = "";
		try
		{
			toggleToChange.isOn = finalState;
			string feedbackName = GetSpokenName(toggleToChange, targetCleanedName);
			actionText = finalState ? "checked" : "unchecked";
			if (toggleToChange.group != null && finalState)
			{
				actionText = "selected";
			}
			Debug.Log($"Voice UI: Set toggle '{toggleToChange.gameObject.name}' (Speaking as: '{feedbackName}') to {finalState}");
			SpeakIfAvailable($"{feedbackName} {actionText}.");
		}
		catch (Exception ex)
		{
			Debug.LogError($"Voice UI: Error setting toggle state for '{toggleToChange.gameObject.name}': {ex.Message}", toggleToChange.gameObject);
			string feedbackName = GetSpokenName(toggleToChange, targetCleanedName);
			SpeakIfAvailable($"Sorry, there was an error changing {feedbackName}.");
		}
	}

	/// <summary>
	/// Sets a slider's value based on voice input like "set [Slider Name] to [Number]".
	/// </summary>
	public void SetSliderValueByName(string dictatedInput)
	{
		if (!CheckTTSSpeaker())
		{
			return;
		}
		Match match = setSliderRegex.Match(dictatedInput);
		if (!match.Success || match.Groups.Count < 4)
		{
			Debug.LogWarning($"SetSliderValueByName: Could not parse: '{dictatedInput}'. Expected 'set [Name] to [Number]'.");
			SpeakIfAvailable($"Sorry, I didn't understand the slider command.");
			return;
		}
		string sliderNamePart = match.Groups[1].Value;
		string numberPart = match.Groups[3].Value;
		string targetCleanedName = FindBestMatchingSliderName(sliderNamePart);
		if (targetCleanedName == null)
		{
			SpeakIfAvailable($"Could not find slider matching {sliderNamePart}.");
			return;
		}
		if (!float.TryParse(numberPart, NumberStyles.Any, CultureInfo.InvariantCulture, out float targetValue))
		{
			Debug.LogWarning($"SetSliderValueByName: Could not parse number '{numberPart}'.");
			SpeakIfAvailable($"Sorry, I couldn't understand the value '{numberPart}'.");
			return;
		}
		if (!interactableSlidersByCleanName.TryGetValue(targetCleanedName, out List<Slider> candidates))
		{
			Debug.LogError($"Voice UI Interaction: Internal error. Key '{targetCleanedName}'.");
			SpeakIfAvailable("Sorry, something went wrong.");
			return;
		}
		Slider sliderToChange = candidates.FirstOrDefault(s => s != null && s.gameObject.activeInHierarchy && s.IsInteractable());
		if (sliderToChange == null)
		{
			bool aebni = candidates.Any(s => s != null && s.gameObject.activeInHierarchy && !s.IsInteractable());
			string fn = GetSpokenName(candidates.FirstOrDefault()?.GetComponent<Component>(), targetCleanedName);
			if (aebni)
			{
				SpeakIfAvailable($"{fn} slider is not interactable.");
			}
			else
			{
				SpeakIfAvailable($"Cannot interact with {fn} slider.");
			}
			return;
		}
		string sliderSpokenName = GetSpokenName(sliderToChange, targetCleanedName);
		if (targetValue < sliderToChange.minValue || targetValue > sliderToChange.maxValue)
		{
			Debug.LogWarning($"SetSliderValueByName: Value {targetValue} out of range [{sliderToChange.minValue} - {sliderToChange.maxValue}] for '{sliderSpokenName}'.");
			SpeakIfAvailable($"Value {targetValue} is outside the range {sliderToChange.minValue} to {sliderToChange.maxValue} for {sliderSpokenName}.");
			return;
		}
		try
		{
			if (sliderToChange.wholeNumbers)
			{
				targetValue = Mathf.Round(targetValue);
			}
			sliderToChange.value = targetValue;
			Debug.Log($"Voice UI: Set slider '{sliderToChange.gameObject.name}' (Speaking as: '{sliderSpokenName}') to {targetValue}");
			SpeakIfAvailable($"{sliderSpokenName} set to {sliderToChange.value}.");
		}
		catch (Exception ex)
		{
			Debug.LogError($"Voice UI: Error setting slider value for '{sliderToChange.gameObject.name}': {ex.Message}", sliderToChange.gameObject);
			SpeakIfAvailable($"Sorry, there was an error setting {sliderSpokenName}.");
		}
	}


	// --- Post-Click Change Detection ---
	/// <summary>
	/// Internal function to handle the button click, state snapshotting, and starting the change check coroutine.
	/// </summary>
	private void PerformClickAndCheckChanges(Button button)
	{
		if (button == null)
		{
			return;
		}
		string nameToSpeak = GetSpokenNameForButton(button);
		Canvas relatedCanvas = FindRelatedCanvas(button);
		bool wasButtonActive = button.gameObject.activeInHierarchy;
		HashSet<GameObject> elementsBefore = GetActiveUIElements(relatedCanvas?.gameObject);
		Debug.Log($"Voice UI: Invoking onClick for button '{button.gameObject.name}' (Speaking as: '{nameToSpeak}')");
		try
		{
			button.onClick.Invoke();
			if (postClickCoroutine != null)
			{
				StopCoroutine(postClickCoroutine);
				postClickCoroutine = null;
			}
			postClickCoroutine = StartCoroutine(CheckUIChangeAfterClick(button, nameToSpeak, relatedCanvas, wasButtonActive, elementsBefore));
		}
		catch (Exception ex)
		{
			Debug.LogError($"Voice UI: Error invoking onClick for button '{button.gameObject.name}': {ex.Message}", button.gameObject);
			SpeakIfAvailable($"Sorry, there was an error interacting with {nameToSpeak}.");
		}
	}

	/// <summary>
	/// Coroutine to check UI state changes after a short delay, provide feedback, and refresh the element dictionaries.
	/// </summary>
	private IEnumerator CheckUIChangeAfterClick(Component clickedComponent, string nameToSpeak, Canvas relatedCanvas, bool wasActiveBefore, HashSet<GameObject> elementsBefore)
	{
		yield return new WaitForSeconds(postClickCheckDelay);
		if (clickedComponent == null || clickedComponent.gameObject == null)
		{
			Debug.Log("VoiceUI CheckUIChange: Clicked component was destroyed.");
			if (ttsSpeaker != null) { ttsSpeaker.Stop(); }
			SpeakIfAvailable($"Clicked {nameToSpeak}.");
			postClickCoroutine = null;
			yield break;
		}
		bool isActiveNow = clickedComponent.gameObject.activeInHierarchy;
		HashSet<GameObject> elementsAfter = GetActiveUIElements(relatedCanvas?.gameObject);
		bool newElementsFound = elementsAfter.Any(elementGO => !elementsBefore.Contains(elementGO));
		if (wasActiveBefore && !isActiveNow && !newElementsFound)
		{
			Debug.Log($"VoiceUI CheckUIChange: Component '{nameToSpeak}' became inactive, no new elements. Announcing closed.");
			if (ttsSpeaker != null) { ttsSpeaker.Stop(); }
			SpeakIfAvailable($"{nameToSpeak} closed.");
		}
		else if (newElementsFound)
		{
			Debug.Log($"VoiceUI CheckUIChange: New elements found after clicking '{nameToSpeak}'. Announcing change.");
			if (ttsSpeaker != null) { ttsSpeaker.Stop(); }
			SpeakIfAvailable($"Clicked {nameToSpeak}.");
			SpeakIfAvailable($"{nameToSpeak} UI changed.");
		}
		else
		{
			if (!(!isActiveNow && wasActiveBefore)) // Avoid double feedback if already said "closed"
			{
				Debug.Log($"VoiceUI CheckUIChange: No significant UI change detected for '{nameToSpeak}'. Standard click feedback.");
				if (ttsSpeaker != null) { ttsSpeaker.Stop(); }
				SpeakIfAvailable($"Clicked {nameToSpeak}.");
			}
		}
		Debug.Log("VoiceUI CheckUIChange: Refreshing interactable element dictionaries.");
		RefreshAllInteractableElements(); // Refresh map after checking changes
		postClickCoroutine = null;
	}

	/// <summary>
	/// Finds the Canvas parent of a component, falling back to the default targetCanvas.
	/// </summary>
	private Canvas FindRelatedCanvas(Component component)
	{
		if (component == null)
		{
			return targetCanvas;
		}
		Canvas canvas = component.GetComponentInParent<Canvas>();
		return canvas != null ? canvas : targetCanvas;
	}

	/// <summary>
	/// Helper to get a set of active relevant UI GameObjects within a scope.
	/// </summary>
	private HashSet<GameObject> GetActiveUIElements(GameObject scope)
	{
		HashSet<GameObject> activeElements = new HashSet<GameObject>();
		GameObject searchRoot = scope;
		if (scope == null && targetCanvas != null)
		{
			searchRoot = targetCanvas.gameObject;
		}
		FindAndAddActiveElements<Button>(searchRoot, activeElements);
		FindAndAddActiveElements<Toggle>(searchRoot, activeElements);
		FindAndAddActiveElements<Slider>(searchRoot, activeElements);
		FindAndAddActiveElements<TMP_InputField>(searchRoot, activeElements);
		FindAndAddActiveElements<TMP_Dropdown>(searchRoot, activeElements);
		if (describeStandaloneText)
		{
			FindAndAddActiveTextElements<TextMeshProUGUI>(searchRoot, activeElements);
			FindAndAddActiveTextElements<Text>(searchRoot, activeElements);
		}
		return activeElements;
	}

	/// <summary>
	/// Generic helper to find components of type T and add their active GameObjects to the set.
	/// </summary>
	private void FindAndAddActiveElements<T>(GameObject searchRoot, HashSet<GameObject> activeSet) where T : Component
	{
		T[] components;
		if (searchRoot != null)
		{
			components = searchRoot.GetComponentsInChildren<T>(true);
		}
		else
		{
			components = FindObjectsByType<T>(FindObjectsSortMode.None);
		}
		foreach (var comp in components)
		{
			if (comp != null && comp.gameObject.activeInHierarchy)
			{
				activeSet.Add(comp.gameObject);
			}
		}
	}

	/// <summary>
	/// Generic helper to find Text components, check for content, and add their active GameObjects to the set.
	/// </summary>
	private void FindAndAddActiveTextElements<T>(GameObject searchRoot, HashSet<GameObject> activeSet) where T : Component
	{
		T[] components;
		if (searchRoot != null)
		{
			components = searchRoot.GetComponentsInChildren<T>(true);
		}
		else
		{
			components = FindObjectsByType<T>(FindObjectsSortMode.None);
		}
		foreach (var comp in components)
		{
			if (comp != null && comp.gameObject.activeInHierarchy)
			{
				string textContent = "";
				if (comp is TextMeshProUGUI tmp) { textContent = tmp.text; }
				else if (comp is Text txt) { textContent = txt.text; }

				if (!string.IsNullOrWhiteSpace(textContent))
				{
					activeSet.Add(comp.gameObject);
				}
			}
		}
	}


	/// <summary>
	/// Helper to get the appropriate spoken name for a button based on settings.
	/// </summary>
	private string GetSpokenNameForButton(Button button)
	{
		if (button == null) { return "unnamed button"; }
		if (elementToSpokenNameMap.TryGetValue(button, out string spokenName)) { return spokenName; }
		Debug.LogWarning($"Button '{button.name}' not found in spoken name map. Determining fallback name.");
		if (preferElementText) { string buttonText = GetButtonText(button); if (!string.IsNullOrWhiteSpace(buttonText)) { return buttonText; } }
		return CleanNameForDictionary(button.gameObject.name);
	}

	/// <summary>
	/// Generic helper to get the spoken name for any component (tries map first).
	/// </summary>
	private string GetSpokenName(Component component, string fallbackCleanedName)
	{
		if (component == null) return fallbackCleanedName ?? "unknown element";
		if (elementToSpokenNameMap.TryGetValue(component, out string spokenName)) { return spokenName; }
		// Fallback if not in map (should be rare after mapping)
		return fallbackCleanedName ?? CleanNameForDictionary(component.gameObject.name);
	}


	// +++ SCREEN READER FUNCTIONALITY (REFACTORED for Sections) +++

	/// <summary>
	/// Describes active UI elements within the default scope, attempting to respect LayoutGroups.
	/// </summary>
	public void DescribeVisibleUI()
	{
		if (postClickCoroutine != null) { StopCoroutine(postClickCoroutine); postClickCoroutine = null; }
		GameObject root = targetCanvas?.gameObject;
		ProcessAndDescribeByHierarchy(root, "visible");
	}

	/// <summary>
	/// Describes active UI elements within a specific Canvas identified by its PARENT's name.
	/// </summary>
	public void DescribeCanvasByName(string parentName)
	{
		if (postClickCoroutine != null) { StopCoroutine(postClickCoroutine); postClickCoroutine = null; }
		if (!CheckTTSSpeaker()) { return; }
		string cleanedParentName = CleanDictatedInput(parentName);
		if (string.IsNullOrWhiteSpace(cleanedParentName)) { SpeakIfAvailable("Please specify a valid UI area name."); return; }
		Debug.Log($"DescribeCanvasByName: Searching for parent name (cleaned): '{cleanedParentName}' (Original Raw Input: '{parentName}')");
		Canvas requestedCanvas = FindObjectsByType<Canvas>(FindObjectsSortMode.None).FirstOrDefault(c => c.transform.parent != null && CleanNameForDictionary(c.transform.parent.gameObject.name).Equals(cleanedParentName) && c.gameObject.activeInHierarchy);
		if (requestedCanvas == null) { SpeakIfAvailable($"Sorry, I couldn't find an active UI area named {parentName}."); return; }
		ProcessAndDescribeByHierarchy(requestedCanvas.gameObject, $"in the {parentName} area");
	}

	/// <summary>
	/// Describes active UI elements within a specified radius of this GameObject's position, sorted by distance.
	/// </summary>
	public void DescribeNearbyUI(float radius = 2.0f)
	{
		if (!CheckTTSSpeaker()) { return; }
		if (postClickCoroutine != null) { StopCoroutine(postClickCoroutine); postClickCoroutine = null; }
		Debug.Log($"VoiceUI DescribeNearbyUI: Searching for UI elements within {radius}m.");
		ProcessAndDescribeScopeFlatSort(null, $"within {radius} meters", false, radius);
	}

	/// <summary>
	/// Determines whether to use hierarchy-based or flat-sorted description.
	/// </summary>
	private void ProcessAndDescribeByHierarchy(GameObject scope, string context)
	{
		GameObject searchRoot = scope;
		if (scope == null && targetCanvas != null) { searchRoot = targetCanvas.gameObject; }
		if (searchRoot == null) { Debug.LogWarning("ProcessAndDescribeByHierarchy: No scope/targetCanvas. Falling back to scene flat sort."); ProcessAndDescribeScopeFlatSort(null, context, true, 0f); return; }

		bool layoutGroupFound = searchRoot.GetComponentInChildren<LayoutGroup>(true) != null;

		if (layoutGroupFound)
		{
			Debug.Log($"ProcessAndDescribeByHierarchy ({context}): LayoutGroup found. Using recursive traversal.");
			List<string> descriptionList = new List<string>();
			HashSet<GameObject> processedObjects = new HashSet<GameObject>();
			ProcessTransformRecursive(searchRoot.transform, descriptionList, processedObjects);
			StringBuilder description = new StringBuilder();
			for (int i = 0; i < descriptionList.Count; i++) { if (i > 0) { description.Append(". "); } description.Append(descriptionList[i]); }
			SpeakDescriptionResult(description, descriptionList.Count, context);
		}
		else
		{
			Debug.Log($"ProcessAndDescribeByHierarchy ({context}): No LayoutGroup found. Falling back to Y-sort.");
			ProcessAndDescribeScopeFlatSort(searchRoot, context, true, 0f);
		}
	}


	/// <summary>
	/// Recursive function to traverse the hierarchy, adding formatted descriptions to a list in traversal order.
	/// </summary>
	private void ProcessTransformRecursive(Transform target, List<string> descriptionList, HashSet<GameObject> processedObjects)
	{
		if (!target.gameObject.activeInHierarchy)
		{
			return;
		}

		// Try to get description for the node itself FIRST
		string elementDescription = GetDescriptionForTransform(target, processedObjects);

		// Add the object to processed AFTER attempting to get its info,
		// but BEFORE recursing children. This prevents infinite loops
		// and ensures children of processed containers (like ToggleGroup) are skipped correctly.
		if (!processedObjects.Add(target.gameObject))
		{
			return; // Already processed (e.g., was a child toggle of an already processed group)
		}

		// If we got a valid description for this node itself, add it
		if (!string.IsNullOrEmpty(elementDescription))
		{
			descriptionList.Add(elementDescription);
		}

		// Now, traverse children IN HIERARCHY ORDER
		if (target.childCount > 0)
		{
			foreach (Transform child in target)
			{
				ProcessTransformRecursive(child, descriptionList, processedObjects);
			}
		}
	}


	/// <summary>
	/// Tries to identify a describable UI element on the target transform and get its info string.
	/// </summary>
	private string GetDescriptionForTransform(Transform target, HashSet<GameObject> processedObjects)
	{
		// [ Code is unchanged ]
		if (!target.TryGetComponent<RectTransform>(out var rectTransform)) return null; string elementType = ""; string nameToSpeak = ""; string status = ""; bool foundElement = false; bool markChildrenProcessed = false; if (target.TryGetComponent<ToggleGroup>(out var group)) { elementType = "ToggleGroup"; string groupName = CleanNameForDictionary(group.gameObject.name); StringBuilder optionsDesc = new StringBuilder(); var togglesInGroup = new List<Toggle>(); foreach (Transform childTransform in group.transform) { if (childTransform.TryGetComponent<Toggle>(out var childToggle) && childToggle.group == group) { togglesInGroup.Add(childToggle); } } if (togglesInGroup.Count == 0) { togglesInGroup = group.GetComponentsInChildren<Toggle>(true).Where(t => t.group == group).ToList(); } int toggleCount = 0; foreach (var tg in togglesInGroup) { if (tg != null && tg.gameObject.activeInHierarchy) { string toggleLabel = GetToggleLabelText(tg) ?? CleanNameForDictionary(tg.gameObject.name); string state = tg.isOn ? "selected" : "not selected"; if (toggleCount > 0) optionsDesc.Append(", "); optionsDesc.Append($"{toggleLabel} {state}"); processedObjects.Add(tg.gameObject); toggleCount++; } } if (toggleCount > 0) { nameToSpeak = $"{groupName}. Options: {optionsDesc}"; } else { nameToSpeak = $"{groupName}. Contains no active toggle options."; } status = ""; foundElement = true; } else if (target.TryGetComponent<Slider>(out var slider)) { elementType = "Slider"; string sliderName = GetSliderLabelText(slider) ?? CleanNameForDictionary(slider.gameObject.name); string valueDesc = slider.wholeNumbers ? $"{slider.value:F0}" : $"{slider.value:F1}"; nameToSpeak = $"{sliderName}, value {valueDesc}, range {slider.minValue:F0} to {slider.maxValue:F0}"; status = describeInactiveState && !slider.interactable ? "inactive" : ""; foundElement = true; } else if (target.TryGetComponent<TMP_InputField>(out var input)) { elementType = "InputField"; string inputName = CleanNameForDictionary(input.gameObject.name); string valueText = string.IsNullOrWhiteSpace(input.text) ? "empty" : input.text; status = describeInactiveState && !input.interactable ? "inactive" : ""; nameToSpeak = $"{inputName}, value {valueText}"; foundElement = true; } else if (target.TryGetComponent<TMP_Dropdown>(out var dropdown)) { elementType = "Dropdown"; string dropdownName = CleanNameForDictionary(dropdown.gameObject.name); string valueText = (dropdown.options != null && dropdown.value >= 0 && dropdown.value < dropdown.options.Count) ? dropdown.options[dropdown.value].text : "no selection"; status = describeInactiveState && !dropdown.interactable ? "inactive" : ""; nameToSpeak = $"{dropdownName}, selected {valueText}"; foundElement = true; } else if (target.TryGetComponent<Toggle>(out var toggle) && toggle.group == null) { if (processedObjects.Contains(toggle.gameObject)) return null; elementType = "Checkbox"; string toggleName = GetToggleLabelText(toggle) ?? CleanNameForDictionary(toggle.gameObject.name); string valueText = toggle.isOn ? "checked" : "unchecked"; status = describeInactiveState && !toggle.interactable ? "inactive" : ""; nameToSpeak = $"{toggleName}, {valueText}"; foundElement = true; } else if (target.TryGetComponent<Button>(out var button)) { if (processedObjects.Contains(button.gameObject)) return null; elementType = "Button"; nameToSpeak = GetSpokenNameForButton(button); status = (describeInactiveState && !button.IsInteractable()) ? "inactive" : ""; foundElement = true; } else if (describeStandaloneText && target.TryGetComponent<TextMeshProUGUI>(out var tmp)) { bool parentIsControl = false; if (tmp.transform.parent != null) { parentIsControl = tmp.transform.parent.GetComponent<Selectable>() != null; } if (!parentIsControl && !string.IsNullOrWhiteSpace(tmp.text)) { elementType = "Text"; nameToSpeak = tmp.text; status = ""; foundElement = true; } } else if (describeStandaloneText && target.TryGetComponent<Text>(out var text)) { bool parentIsControl = false; if (text.transform.parent != null) { parentIsControl = text.transform.parent.GetComponent<Selectable>() != null; } if (!parentIsControl && !string.IsNullOrWhiteSpace(text.text)) { elementType = "Text"; nameToSpeak = text.text; status = ""; foundElement = true; } }
		if (foundElement) { string statusPrefix = !string.IsNullOrEmpty(status) ? status + " " : ""; string typePrefix = ""; switch (elementType) { case "Button": case "Slider": case "Checkbox": case "ToggleGroup": case "InputField": case "Dropdown": typePrefix = elementType + " "; break; default: typePrefix = ""; break; } string sanitizedNameToSpeak = nameToSpeak?.Replace("\n", " ").Replace("\r", "") ?? ""; return $"{statusPrefix}{typePrefix}{sanitizedNameToSpeak}"; }
		return null;
	}


	/// <summary>
	/// [Fallback Method] Core logic to find, sort (by Y or distance), and describe various individual UI elements.
	/// </summary>
	private void ProcessAndDescribeScopeFlatSort(GameObject scope, string context, bool sortByY, float radius = 0f)
	{
		// [ Code is unchanged ]
		if (!CheckTTSSpeaker()) { return; }
		List<UIElementInfo> elementsToDescribe = new List<UIElementInfo>(); HashSet<GameObject> processedControlGOs = new HashSet<GameObject>(); Vector3 centerPosition = this.transform.position; float radiusSq = radius * radius; GameObject searchRoot = scope; if (scope == null && targetCanvas != null) { searchRoot = targetCanvas.gameObject; }
		// Find elements and populate elementsToDescribe List<UIElementInfo>... (Omitted for brevity - see previous version for full finding logic)
		ToggleGroup[] toggleGroups = FindObjectsByType<ToggleGroup>(FindObjectsSortMode.None); foreach (var group in toggleGroups) { if (searchRoot != null && !group.transform.IsChildOf(searchRoot.transform)) continue; if (group != null && group.gameObject.activeInHierarchy && group.TryGetComponent<RectTransform>(out var groupRect)) { if (radius > 0 && Vector3.SqrMagnitude(groupRect.position - centerPosition) > radiusSq) continue; string desc = GetDescriptionForTransform(group.transform, processedControlGOs); if (desc != null) { float dist = (radius > 0) ? Vector3.Distance(centerPosition, groupRect.position) : 0f; elementsToDescribe.Add(new UIElementInfo { RectTransform = groupRect, Distance = dist, ElementType = "ToggleGroup", NameToSpeak = desc, Status = "" }); processedControlGOs.Add(group.gameObject); } } }
		Slider[] sliders = FindObjectsByType<Slider>(FindObjectsSortMode.None); foreach (var slider in sliders) { if (searchRoot != null && !slider.transform.IsChildOf(searchRoot.transform)) continue; if (slider != null && slider.gameObject.activeInHierarchy && slider.TryGetComponent<RectTransform>(out var sliderRect)) { if (processedControlGOs.Contains(slider.gameObject)) continue; if (radius > 0 && Vector3.SqrMagnitude(sliderRect.position - centerPosition) > radiusSq) continue; string desc = GetDescriptionForTransform(slider.transform, processedControlGOs); if (desc != null) { float dist = (radius > 0) ? Vector3.Distance(centerPosition, sliderRect.position) : 0f; string status = describeInactiveState && !slider.interactable ? "inactive" : ""; elementsToDescribe.Add(new UIElementInfo { RectTransform = sliderRect, Distance = dist, ElementType = "Slider", NameToSpeak = desc, Status = status }); processedControlGOs.Add(slider.gameObject); } } }
		TMP_InputField[] inputFields = FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None); foreach (var input in inputFields) { if (searchRoot != null && !input.transform.IsChildOf(searchRoot.transform)) continue; if (input != null && input.gameObject.activeInHierarchy && input.TryGetComponent<RectTransform>(out var inputRect)) { if (processedControlGOs.Contains(input.gameObject)) continue; if (radius > 0 && Vector3.SqrMagnitude(inputRect.position - centerPosition) > radiusSq) continue; string desc = GetDescriptionForTransform(input.transform, processedControlGOs); if (desc != null) { float dist = (radius > 0) ? Vector3.Distance(centerPosition, inputRect.position) : 0f; string status = describeInactiveState && !input.interactable ? "inactive" : ""; elementsToDescribe.Add(new UIElementInfo { RectTransform = inputRect, Distance = dist, ElementType = "InputField", NameToSpeak = desc, Status = status }); processedControlGOs.Add(input.gameObject); } } }
		TMP_Dropdown[] dropdowns = FindObjectsByType<TMP_Dropdown>(FindObjectsSortMode.None); foreach (var dropdown in dropdowns) { if (searchRoot != null && !dropdown.transform.IsChildOf(searchRoot.transform)) continue; if (dropdown != null && dropdown.gameObject.activeInHierarchy && dropdown.TryGetComponent<RectTransform>(out var dropdownRect)) { if (processedControlGOs.Contains(dropdown.gameObject)) continue; if (radius > 0 && Vector3.SqrMagnitude(dropdownRect.position - centerPosition) > radiusSq) continue; string desc = GetDescriptionForTransform(dropdown.transform, processedControlGOs); if (desc != null) { float dist = (radius > 0) ? Vector3.Distance(centerPosition, dropdownRect.position) : 0f; string status = describeInactiveState && !dropdown.interactable ? "inactive" : ""; elementsToDescribe.Add(new UIElementInfo { RectTransform = dropdownRect, Distance = dist, ElementType = "Dropdown", NameToSpeak = desc, Status = status }); processedControlGOs.Add(dropdown.gameObject); } } }
		Toggle[] toggles = FindObjectsByType<Toggle>(FindObjectsSortMode.None); foreach (var toggle in toggles) { if (searchRoot != null && !toggle.transform.IsChildOf(searchRoot.transform)) continue; if (toggle != null && toggle.gameObject.activeInHierarchy && toggle.TryGetComponent<RectTransform>(out var toggleRect)) { if (processedControlGOs.Contains(toggle.gameObject)) continue; if (toggle.group != null) continue; if (radius > 0 && Vector3.SqrMagnitude(toggleRect.position - centerPosition) > radiusSq) continue; string desc = GetDescriptionForTransform(toggle.transform, processedControlGOs); if (desc != null) { float dist = (radius > 0) ? Vector3.Distance(centerPosition, toggleRect.position) : 0f; string status = describeInactiveState && !toggle.interactable ? "inactive" : ""; elementsToDescribe.Add(new UIElementInfo { RectTransform = toggleRect, Distance = dist, ElementType = "Checkbox", NameToSpeak = desc, Status = status }); processedControlGOs.Add(toggle.gameObject); } } }
		Button[] buttons = FindObjectsByType<Button>(FindObjectsSortMode.None); foreach (var button in buttons) { if (searchRoot != null && !button.transform.IsChildOf(searchRoot.transform)) continue; if (button != null && button.gameObject.activeInHierarchy && button.TryGetComponent<RectTransform>(out var buttonRect)) { if (processedControlGOs.Contains(button.gameObject)) continue; if (radius > 0 && Vector3.SqrMagnitude(buttonRect.position - centerPosition) > radiusSq) continue; string desc = GetDescriptionForTransform(button.transform, processedControlGOs); if (desc != null) { float dist = (radius > 0) ? Vector3.Distance(centerPosition, buttonRect.position) : 0f; string status = (describeInactiveState && !button.IsInteractable()) ? "inactive" : ""; elementsToDescribe.Add(new UIElementInfo { RectTransform = buttonRect, Distance = dist, ElementType = "Button", NameToSpeak = desc, Status = status }); processedControlGOs.Add(button.gameObject); } } }
		if (describeStandaloneText) { TextMeshProUGUI[] textComponentsTMP = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None); foreach (var tmp in textComponentsTMP) { if (searchRoot != null && !tmp.transform.IsChildOf(searchRoot.transform)) continue; if (tmp != null && tmp.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(tmp.text) && tmp.TryGetComponent<RectTransform>(out var textRect)) { bool parentIsControl = tmp.transform.parent != null && (tmp.transform.parent.GetComponent<Selectable>() != null); if (processedControlGOs.Contains(tmp.gameObject) || parentIsControl) continue; if (radius > 0 && Vector3.SqrMagnitude(textRect.position - centerPosition) > radiusSq) continue; string desc = GetDescriptionForTransform(tmp.transform, processedControlGOs); if (desc != null) { float dist = (radius > 0) ? Vector3.Distance(centerPosition, textRect.position) : 0f; elementsToDescribe.Add(new UIElementInfo { RectTransform = textRect, Distance = dist, ElementType = "Text", NameToSpeak = desc, Status = "" }); } } } Text[] textComponentsLegacy = FindObjectsByType<Text>(FindObjectsSortMode.None); foreach (var text in textComponentsLegacy) { if (searchRoot != null && !text.transform.IsChildOf(searchRoot.transform)) continue; if (text != null && text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(text.text) && text.TryGetComponent<RectTransform>(out var textRect)) { bool parentIsControl = text.transform.parent != null && (text.transform.parent.GetComponent<Selectable>() != null); if (processedControlGOs.Contains(text.gameObject) || parentIsControl) continue; if (radius > 0 && Vector3.SqrMagnitude(textRect.position - centerPosition) > radiusSq) continue; string desc = GetDescriptionForTransform(text.transform, processedControlGOs); if (desc != null) { float dist = (radius > 0) ? Vector3.Distance(centerPosition, textRect.position) : 0f; elementsToDescribe.Add(new UIElementInfo { RectTransform = textRect, Distance = dist, ElementType = "Text", NameToSpeak = desc, Status = "" }); } } } }
		List<UIElementInfo> sortedElements; if (sortByY) { sortedElements = elementsToDescribe.OrderByDescending(info => info.RectTransform.position.y).ToList(); } else { sortedElements = elementsToDescribe.OrderBy(info => info.Distance).ToList(); }
		StringBuilder description = new StringBuilder(); for (int i = 0; i < sortedElements.Count; i++) { AppendElementDescriptionFromInfo(description, i, sortedElements[i]); }
		SpeakDescriptionResult(description, sortedElements.Count, context);
	}


	/// <summary>
	/// Helper to append a single element's description to the StringBuilder using UIElementInfo.
	/// </summary>
	private void AppendElementDescriptionFromInfo(StringBuilder builder, int index, UIElementInfo elementInfo)
	{
		// [ Code is unchanged ]
		if (index > 0) { builder.Append(". "); }
		string statusPrefix = !string.IsNullOrEmpty(elementInfo.Status) ? elementInfo.Status + " " : ""; string typePrefix = ""; switch (elementInfo.ElementType) { case "Button": case "Slider": case "Checkbox": case "ToggleGroup": case "InputField": case "Dropdown": typePrefix = elementInfo.ElementType + " "; break; default: typePrefix = ""; break; }
		string sanitizedNameToSpeak = elementInfo.NameToSpeak?.Replace("\n", " ").Replace("\r", "") ?? ""; builder.Append($"{statusPrefix}{typePrefix}{sanitizedNameToSpeak}");
	}


	/// <summary>
	/// Helper to speak the final description result.
	/// </summary>
	private void SpeakDescriptionResult(StringBuilder builder, int elementCount, string context) { /* ... Code remains the same ... */ if (elementCount > 0) { SpeakIfAvailable(builder.ToString()); } else { SpeakIfAvailable($"There are no recognized UI elements {context} right now."); } }

	/// <summary>
	/// Helper method to queue text for speaking via the TTSSpeaker. Includes Stop() call first.
	/// </summary>
	private void SpeakIfAvailable(string textToSpeak) { /* ... Code remains the same ... */ if (ttsSpeaker != null && !string.IsNullOrEmpty(textToSpeak)) { ttsSpeaker.Stop(); ttsSpeaker.SpeakQueued(textToSpeak); } else if (ttsSpeaker == null) { Debug.LogError("VoiceUIController: TTSSpeaker not assigned."); } }

	/// <summary>
	/// Helper to check if TTSSpeaker is assigned.
	/// </summary>
	private bool CheckTTSSpeaker() { /* ... Code remains the same ... */ if (ttsSpeaker != null) { return true; } Debug.LogError("VoiceUIController: TTSSpeaker is not assigned."); return false; }

	/// <summary>
	/// Call this method to update the internal mapping of interactable elements.
	/// </summary>
	public void RefreshAllInteractableElements() { /* ... Code remains the same ... */ Debug.Log("VoiceUIController: Refreshing interactable element dictionaries."); MapAllInteractableElements(); }
}
