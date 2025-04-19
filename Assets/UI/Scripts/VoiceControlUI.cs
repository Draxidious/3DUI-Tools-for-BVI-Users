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
/// Finds various active UI elements, maps interactable ones by name/text (using heuristics),
/// triggers actions (clicks, toggles, slider sets, text input, dropdown selection), and describes UI content sequentially.
/// String comparisons ignore case, internal whitespace, and punctuation. Attempts internal detection
/// of UI changes after button clicks for basic context feedback and automatically refreshes the
/// interactable element dictionaries after clicks. Includes TTS chunking for long descriptions.
/// </summary>
public class VoiceUIController : MonoBehaviour
{
	// Helper struct to hold information about UI elements
	private struct UIElementInfo
	{
		public RectTransform RectTransform; public float Distance; public string ElementType; public string NameToSpeak; public string Status;
	}

	// Regex definitions...
	private static readonly Regex nameCleaningRegex = new Regex(@"(\s*\(\d+\)|\d+)$", RegexOptions.Compiled);
	private static readonly Regex whitespaceRemovalRegex = new Regex(@"\s+", RegexOptions.Compiled);
	private static readonly Regex punctuationRemovalRegex = new Regex(@"[\p{P}]", RegexOptions.Compiled);


	[Header("TTS Settings")]
	public TTSSpeaker ttsSpeaker;
	[Tooltip("Maximum characters per TTS chunk to avoid service limits.")]
	public int ttsMaxChunkLength = 250;

	[Header("UI Settings")]
	public Canvas targetCanvas;
	public bool preferElementText = true;
	public bool describeStandaloneText = true;
	public bool describeInactiveState = true;
	[Tooltip("Ignore placeholder text when describing Input Fields.")]
	public bool ignorePlaceholderWhenReading = true;
	public float postClickCheckDelay = 0.02f;

	// Dictionaries for MAPPING interactable elements by cleaned name
	private Dictionary<string, List<Button>> interactableButtonsByCleanName = new Dictionary<string, List<Button>>();
	private Dictionary<string, List<Toggle>> interactableTogglesByCleanName = new Dictionary<string, List<Toggle>>();
	private Dictionary<string, List<Slider>> interactableSlidersByCleanName = new Dictionary<string, List<Slider>>();
	private Dictionary<string, List<TMP_InputField>> interactableInputFieldsByCleanName = new Dictionary<string, List<TMP_InputField>>();
	private Dictionary<string, List<TMP_Dropdown>> interactableDropdownsByCleanName = new Dictionary<string, List<TMP_Dropdown>>(); // Added for Dropdowns

	// Dictionary for storing original SPOKEN names
	private Dictionary<Component, string> elementToSpokenNameMap = new Dictionary<Component, string>();

	// Coroutine reference
	private Coroutine postClickCoroutine = null;


	void Start()
	{
		if (ttsSpeaker == null) { ttsSpeaker = GetComponent<TTSSpeaker>(); if (ttsSpeaker == null) { Debug.LogError("VoiceUIController: TTSSpeaker not assigned!"); } }
		MapAllInteractableElements();
	}

	/// <summary>
	/// Finds and maps all supported interactable UI elements (Buttons, Toggles, Sliders, InputFields, Dropdowns).
	/// </summary>
	public void MapAllInteractableElements()
	{
		// Clear existing maps
		interactableButtonsByCleanName.Clear();
		interactableTogglesByCleanName.Clear();
		interactableSlidersByCleanName.Clear();
		interactableInputFieldsByCleanName.Clear();
		interactableDropdownsByCleanName.Clear(); // Clear dropdown map
		elementToSpokenNameMap.Clear();

		string scopeMsg = (targetCanvas == null) ? "entire scene" : $"Canvas '{targetCanvas.name}'";
		Debug.Log($"MapAllInteractableElements: Scanning {scopeMsg}...");

		int buttonMappedCount = 0;
		int toggleMappedCount = 0;
		int sliderMappedCount = 0;
		int inputFieldMappedCount = 0;
		int dropdownMappedCount = 0; // Added count

		// --- Map Buttons ---
		Button[] allButtons;
		if (targetCanvas != null) { allButtons = targetCanvas.GetComponentsInChildren<Button>(true); }
		else { allButtons = FindObjectsByType<Button>(FindObjectsSortMode.None); }
		foreach (var button in allButtons) { /* ... button mapping logic ... */ string oN = button.gameObject.name, n2U = oN, sN = oN; if (preferElementText) { string fT = GetButtonText(button); if (!string.IsNullOrWhiteSpace(fT)) { n2U = fT; sN = fT; } else { sN = oN; } } else { sN = oN; } string cN = CleanNameForDictionary(n2U); if (string.IsNullOrEmpty(cN) || cN == "unnameduielement") continue; if (!interactableButtonsByCleanName.TryGetValue(cN, out List<Button> bL)) { bL = new List<Button>(); interactableButtonsByCleanName.Add(cN, bL); } if (!bL.Contains(button)) bL.Add(button); elementToSpokenNameMap[button] = sN; buttonMappedCount++; }

		// --- Map Toggles ---
		Toggle[] allToggles;
		if (targetCanvas != null) { allToggles = targetCanvas.GetComponentsInChildren<Toggle>(true); }
		else { allToggles = FindObjectsByType<Toggle>(FindObjectsSortMode.None); }
		foreach (var toggle in allToggles) { /* ... toggle mapping logic ... */ string oN = toggle.gameObject.name, n2U = oN, sN = oN; string lT = GetToggleLabelText(toggle); if (preferElementText && !string.IsNullOrWhiteSpace(lT)) { n2U = lT; sN = lT; } else { sN = oN; } string cN = CleanNameForDictionary(n2U); if (string.IsNullOrEmpty(cN) || cN == "unnameduielement") continue; if (!interactableTogglesByCleanName.TryGetValue(cN, out List<Toggle> tL)) { tL = new List<Toggle>(); interactableTogglesByCleanName.Add(cN, tL); } if (!tL.Contains(toggle)) tL.Add(toggle); elementToSpokenNameMap[toggle] = sN; toggleMappedCount++; }

		// --- Map Sliders ---
		Slider[] allSliders;
		if (targetCanvas != null) { allSliders = targetCanvas.GetComponentsInChildren<Slider>(true); }
		else { allSliders = FindObjectsByType<Slider>(FindObjectsSortMode.None); }
		foreach (var slider in allSliders) { /* ... slider mapping logic ... */ string oN = slider.gameObject.name, n2U = oN, sN = oN; string lT = GetSliderLabelText(slider); if (preferElementText && !string.IsNullOrWhiteSpace(lT)) { n2U = lT; sN = lT; } else { sN = oN; } string cN = CleanNameForDictionary(n2U); if (string.IsNullOrEmpty(cN) || cN == "unnameduielement") continue; if (!interactableSlidersByCleanName.TryGetValue(cN, out List<Slider> sL)) { sL = new List<Slider>(); interactableSlidersByCleanName.Add(cN, sL); } if (!sL.Contains(slider)) sL.Add(slider); elementToSpokenNameMap[slider] = sN; sliderMappedCount++; }

		// --- Map Input Fields (TMP) ---
		TMP_InputField[] allInputFields;
		if (targetCanvas != null) { allInputFields = targetCanvas.GetComponentsInChildren<TMP_InputField>(true); }
		else { allInputFields = FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None); }
		foreach (var inputField in allInputFields) { /* ... input field mapping logic ... */ string oN = inputField.gameObject.name, n2U = oN, sN = oN; string lT = GetInputFieldLabelText(inputField); if (preferElementText && !string.IsNullOrWhiteSpace(lT)) { n2U = lT; sN = lT; } else { sN = oN; } string cN = CleanNameForDictionary(n2U); if (string.IsNullOrEmpty(cN) || cN == "unnameduielement") continue; if (!interactableInputFieldsByCleanName.TryGetValue(cN, out List<TMP_InputField> iL)) { iL = new List<TMP_InputField>(); interactableInputFieldsByCleanName.Add(cN, iL); } if (!iL.Contains(inputField)) iL.Add(inputField); elementToSpokenNameMap[inputField] = sN; inputFieldMappedCount++; }

		// --- Map Dropdowns (TMP) ---
		TMP_Dropdown[] allDropdowns;
		if (targetCanvas != null) { allDropdowns = targetCanvas.GetComponentsInChildren<TMP_Dropdown>(true); }
		else { allDropdowns = FindObjectsByType<TMP_Dropdown>(FindObjectsSortMode.None); }

		foreach (var dropdown in allDropdowns)
		{
			string originalName = dropdown.gameObject.name;
			string baseName = originalName;
			string spokenName = originalName;

			string labelText = GetDropdownLabelText(dropdown); // Use specific helper
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

			if (!string.IsNullOrEmpty(cleanedBaseName) && cleanedBaseName != "unnameduielement")
			{
				if (!interactableDropdownsByCleanName.TryGetValue(cleanedBaseName, out List<TMP_Dropdown> dropdownList))
				{
					dropdownList = new List<TMP_Dropdown>();
					interactableDropdownsByCleanName.Add(cleanedBaseName, dropdownList);
				}
				if (!dropdownList.Contains(dropdown))
				{
					dropdownList.Add(dropdown);
				}
				elementToSpokenNameMap[dropdown] = spokenName;
				dropdownMappedCount++;
			}
		}

		Debug.Log($"MapAllInteractableElements: Mapped {buttonMappedCount} Btns ({interactableButtonsByCleanName.Count} names), " +
				  $"{toggleMappedCount} Tgls ({interactableTogglesByCleanName.Count} names), " +
				  $"{sliderMappedCount} Sliders ({interactableSlidersByCleanName.Count} names), " +
				  $"{inputFieldMappedCount} InputFields ({interactableInputFieldsByCleanName.Count} names), " +
				  $"{dropdownMappedCount} Dropdowns ({interactableDropdownsByCleanName.Count} names) from {scopeMsg}.");
	}

	// --- Helpers for getting text/labels ---
	private string GetButtonText(Button button) { /* ... Code remains the same ... */ if (button == null) { return null; } TextMeshProUGUI bt = button.GetComponentInChildren<TextMeshProUGUI>(); if (bt != null && !string.IsNullOrWhiteSpace(bt.text)) { return bt.text; } Text bl = button.GetComponentInChildren<Text>(); if (bl != null && !string.IsNullOrWhiteSpace(bl.text)) { return bl.text; } return null; }
	private string GetToggleLabelText(Toggle toggle) { /* ... Code remains the same ... */ if (toggle == null) { return null; } TextMeshProUGUI lt = toggle.GetComponentInChildren<TextMeshProUGUI>(); if (lt != null && !string.IsNullOrWhiteSpace(lt.text)) { return lt.text; } Text ll = toggle.GetComponentInChildren<Text>(); if (ll != null && !string.IsNullOrWhiteSpace(ll.text)) { return ll.text; } Transform p = toggle.transform.parent; if (p != null) { foreach (Transform s in p) { if (s == toggle.transform) continue; TextMeshProUGUI st = s.GetComponent<TextMeshProUGUI>(); if (st != null && !string.IsNullOrWhiteSpace(st.text)) return st.text; Text sl = s.GetComponent<Text>(); if (sl != null && !string.IsNullOrWhiteSpace(sl.text)) return sl.text; } } return null; }
	private string GetSliderLabelText(Slider slider) { /* ... Code remains the same ... */ if (slider == null || slider.transform.parent == null) { return null; } Transform parent = slider.transform.parent; int sliderIndex = slider.transform.GetSiblingIndex(); if (sliderIndex > 0) { Transform potentialLabelSibling = parent.GetChild(sliderIndex - 1); if (potentialLabelSibling != null && potentialLabelSibling.gameObject.activeInHierarchy) { TextMeshProUGUI tmpLabel = potentialLabelSibling.GetComponent<TextMeshProUGUI>(); if (tmpLabel != null && !string.IsNullOrWhiteSpace(tmpLabel.text)) { return tmpLabel.text; } Text legacyLabel = potentialLabelSibling.GetComponent<Text>(); if (legacyLabel != null && !string.IsNullOrWhiteSpace(legacyLabel.text)) { return legacyLabel.text; } } } return null; }
	private string GetInputFieldLabelText(TMP_InputField inputField) { /* ... Code remains the same ... */ if (inputField == null || inputField.transform.parent == null) { return null; } Transform parent = inputField.transform.parent; int inputIndex = inputField.transform.GetSiblingIndex(); if (inputIndex > 0) { Transform potentialLabelSibling = parent.GetChild(inputIndex - 1); if (potentialLabelSibling != null && potentialLabelSibling.gameObject.activeInHierarchy) { TextMeshProUGUI tmpLabel = potentialLabelSibling.GetComponent<TextMeshProUGUI>(); if (tmpLabel != null && !string.IsNullOrWhiteSpace(tmpLabel.text)) { return tmpLabel.text; } Text legacyLabel = potentialLabelSibling.GetComponent<Text>(); if (legacyLabel != null && !string.IsNullOrWhiteSpace(legacyLabel.text)) { return legacyLabel.text; } } } return null; }

	/// <summary>
	/// NEW: Helper to find a label for a Dropdown.
	/// Looks for an active Text/TMP_Text on the sibling GameObject immediately preceding it.
	/// </summary>
	private string GetDropdownLabelText(TMP_Dropdown dropdown)
	{
		if (dropdown == null || dropdown.transform.parent == null)
		{
			return null;
		}
		Transform parent = dropdown.transform.parent;
		int dropdownIndex = dropdown.transform.GetSiblingIndex();

		if (dropdownIndex > 0)
		{
			Transform potentialLabelSibling = parent.GetChild(dropdownIndex - 1);
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
		return null; // No preceding sibling label found
	}


	// --- Helpers for cleaning names ---
	private string CleanNameForDictionary(string originalName) { /* ... Code remains the same ... */ if (string.IsNullOrEmpty(originalName)) { return string.Empty; } string l = originalName.ToLowerInvariant(), c = nameCleaningRegex.Replace(l, ""), p = punctuationRemovalRegex.Replace(c, ""), w = whitespaceRemovalRegex.Replace(p, ""), f = w.Trim(); return string.IsNullOrEmpty(f) ? "unnameduielement" : f; }
	private string CleanDictatedInput(string dictatedInput) { /* ... Code remains the same ... */ if (string.IsNullOrEmpty(dictatedInput)) { return string.Empty; } string l = dictatedInput.ToLowerInvariant(), p = punctuationRemovalRegex.Replace(l, ""), w = whitespaceRemovalRegex.Replace(p, ""); return w.Trim(); }

	// --- Interaction Logic ---

	// FindBestMatching helpers
	private string FindBestMatchingCleanedName(string dictatedInput, Dictionary<string, List<Button>> buttonDict) { /* ... Code remains the same ... */ string ci = CleanDictatedInput(dictatedInput); if (string.IsNullOrEmpty(ci)) { return null; } string bm = null; int lml = 0; foreach (string kcn in buttonDict.Keys) { if (ci.StartsWith(kcn, StringComparison.Ordinal)) { if (kcn.Length > lml) { lml = kcn.Length; bm = kcn; } } } return bm; }
	private string FindBestMatchingToggleName(string dictatedInput) { /* ... Code remains the same ... */ string ci = CleanDictatedInput(dictatedInput); if (string.IsNullOrEmpty(ci)) { return null; } string bm = null; int lml = 0; foreach (string kcn in interactableTogglesByCleanName.Keys) { if (ci.StartsWith(kcn, StringComparison.Ordinal)) { if (kcn.Length > lml) { lml = kcn.Length; bm = kcn; } } } return bm; }
	private string FindBestMatchingSliderName(string dictatedInput) { /* ... Code remains the same ... */ string ci = CleanDictatedInput(dictatedInput); if (string.IsNullOrEmpty(ci)) { return null; } string bm = null; int lml = 0; foreach (string kcn in interactableSlidersByCleanName.Keys) { if (ci.StartsWith(kcn, StringComparison.Ordinal)) { if (kcn.Length > lml) { lml = kcn.Length; bm = kcn; } } } return bm; }
	private string FindBestMatchingInputFieldByName(string dictatedInput) { /* ... Code remains the same ... */ string ci = CleanDictatedInput(dictatedInput); if (string.IsNullOrEmpty(ci)) { return null; } string bm = null; int lml = 0; foreach (string kcn in interactableInputFieldsByCleanName.Keys) { if (ci.StartsWith(kcn, StringComparison.Ordinal)) { if (kcn.Length > lml) { lml = kcn.Length; bm = kcn; } } } return bm; }
	// NEW: Helper for Dropdowns
	private string FindBestMatchingDropdownByName(string dictatedInput)
	{
		string cleanedInput = CleanDictatedInput(dictatedInput);
		if (string.IsNullOrEmpty(cleanedInput)) { return null; }

		string bestMatch = null;
		int longestMatchLength = 0;

		foreach (string knownCleanedName in interactableDropdownsByCleanName.Keys)
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


	// IsInteractable checks
	public bool IsUIButtonInteractable(string dictatedInput) { /* ... Code remains the same ... */ string bm = FindBestMatchingCleanedName(dictatedInput, interactableButtonsByCleanName); if (bm != null && interactableButtonsByCleanName.TryGetValue(bm, out var c)) { return c.Any(b => b != null && b.gameObject.activeInHierarchy && b.IsInteractable()); } return false; }
	public bool IsUIToggleInteractable(string dictatedInput) { /* ... Code remains the same ... */ string bm = FindBestMatchingToggleName(dictatedInput); if (bm != null && interactableTogglesByCleanName.TryGetValue(bm, out var c)) { return c.Any(t => t != null && t.gameObject.activeInHierarchy && t.IsInteractable()); } return false; }
	public bool IsUISliderInteractable(string dictatedInput) { /* ... Code remains the same ... */ string bm = FindBestMatchingSliderName(dictatedInput); if (bm != null && interactableSlidersByCleanName.TryGetValue(bm, out var c)) { return c.Any(s => s != null && s.gameObject.activeInHierarchy && s.IsInteractable()); } return false; }
	public bool IsUIInputFieldInteractable(string dictatedInput) { /* ... Code remains the same ... */ string bm = FindBestMatchingInputFieldByName(dictatedInput); if (bm != null && interactableInputFieldsByCleanName.TryGetValue(bm, out var c)) { return c.Any(i => i != null && i.gameObject.activeInHierarchy && i.IsInteractable() && !i.readOnly); } return false; }
	// NEW: Check for Dropdowns
	public bool IsUIDropdownInteractable(string dictatedInput)
	{
		string bestMatch = FindBestMatchingDropdownByName(dictatedInput);
		if (bestMatch != null && interactableDropdownsByCleanName.TryGetValue(bestMatch, out var candidates))
		{
			return candidates.Any(d => d != null && d.gameObject.activeInHierarchy && d.IsInteractable());
		}
		return false;
	}

	// Interaction execution functions
	public void InteractWithUIButtonByName(string dictatedInput) { /* ... Code remains the same ... */ string tcn = FindBestMatchingCleanedName(dictatedInput, interactableButtonsByCleanName); if (tcn == null) { SpeakIfAvailable($"Could not find button matching {dictatedInput}."); return; } if (!interactableButtonsByCleanName.TryGetValue(tcn, out List<Button> c)) { Debug.LogError($"Voice UI Interaction: Internal error. Key '{tcn}'."); SpeakIfAvailable("Sorry, something went wrong."); return; } Button btc = c.FirstOrDefault(b => b != null && b.gameObject.activeInHierarchy && b.IsInteractable()); if (btc == null) { bool aebni = c.Any(b => b != null && b.gameObject.activeInHierarchy && !b.IsInteractable()); string fn = GetSpokenName(c.FirstOrDefault()?.GetComponent<Component>(), tcn); if (aebni) { SpeakIfAvailable($"{fn} button is not interactable right now."); } else { SpeakIfAvailable($"Cannot interact with {fn} button right now."); } return; } PerformClickAndCheckChanges(btc); }
	public void CheckCheckboxByName(string dictatedName) { /* ... Code remains the same ... */ SetToggleState(dictatedName, true); }
	public void UncheckCheckboxByName(string dictatedName) { /* ... Code remains the same ... */ SetToggleState(dictatedName, false); }
	public void SelectToggleInGroupByName(string dictatedName) { /* ... Code remains the same ... */ SetToggleState(dictatedName, true); }
	private void SetToggleState(string dictatedName, bool desiredState) { /* ... Code remains the same ... */ string tcn = FindBestMatchingToggleName(dictatedName); if (tcn == null) { SpeakIfAvailable($"Could not find toggle or checkbox matching {dictatedName}."); return; } if (!interactableTogglesByCleanName.TryGetValue(tcn, out List<Toggle> c)) { Debug.LogError($"Voice UI Interaction: Internal error. Key '{tcn}'."); SpeakIfAvailable("Sorry, something went wrong."); return; } Toggle ttc = c.FirstOrDefault(t => t != null && t.gameObject.activeInHierarchy && t.IsInteractable()); if (ttc == null) { bool aebni = c.Any(t => t != null && t.gameObject.activeInHierarchy && !t.IsInteractable()); string fn = GetSpokenName(c.FirstOrDefault()?.GetComponent<Component>(), tcn); if (aebni) { SpeakIfAvailable($"{fn} is not interactable right now."); } else { SpeakIfAvailable($"Cannot interact with {fn} right now."); } return; } bool fs = desiredState; if (ttc.isOn == fs) { string cs = fs ? "already checked" : "already unchecked"; if (ttc.group != null && fs) { cs = "already selected"; } string fn = GetSpokenName(ttc, tcn); SpeakIfAvailable($"{fn} is {cs}."); return; } string at = ""; try { ttc.isOn = fs; string fn = GetSpokenName(ttc, tcn); at = fs ? "checked" : "unchecked"; if (ttc.group != null && fs) { at = "selected"; } Debug.Log($"Voice UI: Set toggle '{ttc.gameObject.name}' (Speaking as: '{fn}') to {fs}"); SpeakIfAvailable($"{fn} {at}."); } catch (Exception ex) { Debug.LogError($"Voice UI: Error setting toggle state for '{ttc.gameObject.name}': {ex.Message}", ttc.gameObject); string fn = GetSpokenName(ttc, tcn); SpeakIfAvailable($"Sorry, there was an error changing {fn}."); } }
	public void SetSliderValueByName(string sliderName, int targetValue) { /* ... Code remains the same ... */ if (!CheckTTSSpeaker()) return; string targetCleanedName = FindBestMatchingSliderName(sliderName); if (targetCleanedName == null) { SpeakIfAvailable($"Could not find slider matching {sliderName}."); return; } if (!interactableSlidersByCleanName.TryGetValue(targetCleanedName, out List<Slider> candidates)) { Debug.LogError($"Voice UI Interaction: Internal error. Key '{targetCleanedName}'."); SpeakIfAvailable("Sorry, something went wrong."); return; } Slider sliderToChange = candidates.FirstOrDefault(s => s != null && s.gameObject.activeInHierarchy && s.IsInteractable()); if (sliderToChange == null) { bool aebni = candidates.Any(s => s != null && s.gameObject.activeInHierarchy && !s.IsInteractable()); string fn = GetSpokenName(candidates.FirstOrDefault()?.GetComponent<Component>(), targetCleanedName); if (aebni) { SpeakIfAvailable($"{fn} slider is not interactable."); } else { SpeakIfAvailable($"Cannot interact with {fn} slider."); } return; } string sliderSpokenName = GetSpokenName(sliderToChange, targetCleanedName); if (targetValue < sliderToChange.minValue || targetValue > sliderToChange.maxValue) { Debug.LogWarning($"SetSliderValueByName: Value {targetValue} out of range [{sliderToChange.minValue} - {sliderToChange.maxValue}] for '{sliderSpokenName}'."); SpeakIfAvailable($"Value {targetValue.ToString(CultureInfo.InvariantCulture)} is outside the range {sliderToChange.minValue.ToString("F0", CultureInfo.InvariantCulture)} to {sliderToChange.maxValue.ToString("F0", CultureInfo.InvariantCulture)} for {sliderSpokenName}."); return; } try { sliderToChange.value = targetValue; Debug.Log($"Voice UI: Set slider '{sliderToChange.gameObject.name}' (Speaking as: '{sliderSpokenName}') to {targetValue}"); SpeakIfAvailable($"{sliderSpokenName} set to {sliderToChange.value.ToString(CultureInfo.InvariantCulture)}."); } catch (Exception ex) { Debug.LogError($"Voice UI: Error setting slider value for '{sliderToChange.gameObject.name}': {ex.Message}", sliderToChange.gameObject); SpeakIfAvailable($"Sorry, there was an error setting {sliderSpokenName}."); } }
	public void SetTextInputValueByName(string inputFieldName, string targetValue) { /* ... Code remains the same ... */ if (!CheckTTSSpeaker()) return; string targetCleanedName = FindBestMatchingInputFieldByName(inputFieldName); if (targetCleanedName == null) { SpeakIfAvailable($"Could not find input field matching {inputFieldName}."); return; } if (!interactableInputFieldsByCleanName.TryGetValue(targetCleanedName, out List<TMP_InputField> candidates)) { Debug.LogError($"Voice UI Interaction: Internal error. Key '{targetCleanedName}'."); SpeakIfAvailable("Sorry, something went wrong."); return; } TMP_InputField inputToChange = candidates.FirstOrDefault(i => i != null && i.gameObject.activeInHierarchy && i.IsInteractable() && !i.readOnly); if (inputToChange == null) { bool aebni = candidates.Any(i => i != null && i.gameObject.activeInHierarchy && (!i.IsInteractable() || i.readOnly)); string feedbackName = GetSpokenName(candidates.FirstOrDefault()?.GetComponent<Component>(), targetCleanedName); if (aebni) { SpeakIfAvailable($"{feedbackName} input field is not interactable."); } else { SpeakIfAvailable($"Cannot interact with {feedbackName} input field."); } return; } string inputSpokenName = GetSpokenName(inputToChange, targetCleanedName); try { inputToChange.text = targetValue; Debug.Log($"Voice UI: Set input field '{inputToChange.gameObject.name}' (Speaking as: '{inputSpokenName}') to '{targetValue}'"); if (inputToChange.contentType == TMP_InputField.ContentType.Password) { SpeakIfAvailable($"{inputSpokenName} set."); } else { SpeakIfAvailable($"{inputSpokenName} set to {targetValue}."); } } catch (Exception ex) { Debug.LogError($"Voice UI: Error setting input field value for '{inputToChange.gameObject.name}': {ex.Message}", inputToChange.gameObject); SpeakIfAvailable($"Sorry, there was an error setting {inputSpokenName}."); } }

	/// <summary>
	/// NEW: Selects an option in a Dropdown based on the option's visible text.
	/// </summary>
	/// <param name="dropdownName">The dictated name matching the dropdown's label or GameObject name.</param>
	/// <param name="targetOptionText">The text of the option to select.</param>
	public void SelectDropdownOptionByName(string dropdownName, string targetOptionText)
	{
		if (!CheckTTSSpeaker())
		{
			return;
		}

		// 1. Clean the incoming dropdown name and find match
		string targetCleanedName = FindBestMatchingDropdownByName(dropdownName); // Use specific helper
		if (targetCleanedName == null)
		{
			SpeakIfAvailable($"Could not find dropdown matching {dropdownName}.");
			return;
		}

		// 2. Find the target Dropdown component
		if (!interactableDropdownsByCleanName.TryGetValue(targetCleanedName, out List<TMP_Dropdown> candidates))
		{
			Debug.LogError($"Voice UI Interaction: Internal error. Found key '{targetCleanedName}' but no dropdown list.");
			SpeakIfAvailable("Sorry, something went wrong finding the dropdown.");
			return;
		}

		TMP_Dropdown dropdownToChange = candidates.FirstOrDefault(d => d != null && d.gameObject.activeInHierarchy && d.IsInteractable());

		if (dropdownToChange == null)
		{
			bool anyExistButNotInteractable = candidates.Any(d => d != null && d.gameObject.activeInHierarchy && !d.IsInteractable());
			string feedbackName = GetSpokenName(candidates.FirstOrDefault()?.GetComponent<Component>(), targetCleanedName);
			if (anyExistButNotInteractable) { SpeakIfAvailable($"{feedbackName} dropdown is not interactable right now."); }
			else { SpeakIfAvailable($"Cannot interact with {feedbackName} dropdown right now."); }
			return;
		}

		// 3. Find the index of the target option text (case-insensitive)
		int targetIndex = -1;
		if (dropdownToChange.options != null)
		{
			// Use case-insensitive comparison for matching spoken option text
			targetIndex = dropdownToChange.options.FindIndex(option =>
				option.text.Equals(targetOptionText, StringComparison.OrdinalIgnoreCase)
			);
		}

		// 4. Validate the index
		string dropdownSpokenName = GetSpokenName(dropdownToChange, targetCleanedName);
		if (targetIndex < 0)
		{
			Debug.LogWarning($"SelectDropdownOptionByName: Option '{targetOptionText}' not found in dropdown '{dropdownSpokenName}'.");
			SpeakIfAvailable($"Option {targetOptionText} not found in {dropdownSpokenName}.");
			return;
		}

		// 5. Set the value and provide feedback
		try
		{
			// Check if already selected
			if (dropdownToChange.value == targetIndex)
			{
				SpeakIfAvailable($"{targetOptionText} is already selected in {dropdownSpokenName}.");
				return;
			}

			dropdownToChange.value = targetIndex;
			// Refresh shown value immediately (optional, but good practice)
			dropdownToChange.RefreshShownValue();

			Debug.Log($"Voice UI: Set dropdown '{dropdownToChange.gameObject.name}' (Speaking as: '{dropdownSpokenName}') to option '{targetOptionText}' (index {targetIndex})");
			SpeakIfAvailable($"{dropdownSpokenName} set to {targetOptionText}.");
		}
		catch (Exception ex)
		{
			Debug.LogError($"Voice UI: Error setting dropdown value for '{dropdownToChange.gameObject.name}': {ex.Message}", dropdownToChange.gameObject);
			SpeakIfAvailable($"Sorry, there was an error selecting {targetOptionText} in {dropdownSpokenName}.");
		}
	}


	// --- Post-Click Change Detection ---
	private void PerformClickAndCheckChanges(Button button) { /* ... Code remains the same ... */ if (button == null) { return; } string nts = GetSpokenNameForButton(button); Canvas rc = FindRelatedCanvas(button); bool wab = button.gameObject.activeInHierarchy; HashSet<GameObject> eb = GetActiveUIElements(rc?.gameObject); Debug.Log($"Voice UI: Invoking onClick for button '{button.gameObject.name}' (Speaking as: '{nts}')"); try { button.onClick.Invoke(); if (postClickCoroutine != null) { StopCoroutine(postClickCoroutine); postClickCoroutine = null; } postClickCoroutine = StartCoroutine(CheckUIChangeAfterClick(button, nts, rc, wab, eb)); } catch (Exception ex) { Debug.LogError($"Voice UI: Error invoking onClick for button '{button.gameObject.name}': {ex.Message}", button.gameObject); SpeakIfAvailable($"Sorry, there was an error interacting with {nts}."); } }
	private IEnumerator CheckUIChangeAfterClick(Component clickedComponent, string nameToSpeak, Canvas relatedCanvas, bool wasActiveBefore, HashSet<GameObject> elementsBefore) { /* ... Code remains the same ... */ yield return new WaitForSeconds(postClickCheckDelay); if (clickedComponent == null || clickedComponent.gameObject == null) { Debug.Log("VoiceUI CheckUIChange: Clicked component was destroyed."); if (ttsSpeaker != null) { ttsSpeaker.Stop(); } SpeakIfAvailable($"Clicked {nameToSpeak}."); postClickCoroutine = null; yield break; } bool isActiveNow = clickedComponent.gameObject.activeInHierarchy; HashSet<GameObject> elementsAfter = GetActiveUIElements(relatedCanvas?.gameObject); bool newElementsFound = elementsAfter.Any(elementGO => !elementsBefore.Contains(elementGO)); if (wasActiveBefore && !isActiveNow && !newElementsFound) { Debug.Log($"VoiceUI CheckUIChange: Component '{nameToSpeak}' became inactive, no new elements. Announcing closed."); if (ttsSpeaker != null) { ttsSpeaker.Stop(); } SpeakIfAvailable($"{nameToSpeak} closed."); } else if (newElementsFound) { Debug.Log($"VoiceUI CheckUIChange: New elements found after clicking '{nameToSpeak}'. Announcing change."); if (ttsSpeaker != null) { ttsSpeaker.Stop(); } SpeakIfAvailable($"Clicked {nameToSpeak}."); SpeakIfAvailable($"{nameToSpeak} UI changed."); } else { if (!(!isActiveNow && wasActiveBefore)) { Debug.Log($"VoiceUI CheckUIChange: No significant UI change detected for '{nameToSpeak}'. Standard click feedback."); if (ttsSpeaker != null) { ttsSpeaker.Stop(); } SpeakIfAvailable($"Clicked {nameToSpeak}."); } } Debug.Log("VoiceUI CheckUIChange: Refreshing interactable element dictionaries."); RefreshAllInteractableElements(); postClickCoroutine = null; }
	private Canvas FindRelatedCanvas(Component component) { /* ... Code remains the same ... */ if (component == null) { return targetCanvas; } Canvas canvas = component.GetComponentInParent<Canvas>(); return canvas != null ? canvas : targetCanvas; }
	private HashSet<GameObject> GetActiveUIElements(GameObject scope) { /* ... Code remains the same ... */ HashSet<GameObject> activeElements = new HashSet<GameObject>(); GameObject searchRoot = scope; if (scope == null && targetCanvas != null) { searchRoot = targetCanvas.gameObject; } FindAndAddActiveElements<Button>(searchRoot, activeElements); FindAndAddActiveElements<Toggle>(searchRoot, activeElements); FindAndAddActiveElements<Slider>(searchRoot, activeElements); FindAndAddActiveElements<TMP_InputField>(searchRoot, activeElements); FindAndAddActiveElements<TMP_Dropdown>(searchRoot, activeElements); if (describeStandaloneText) { FindAndAddActiveTextElements<TextMeshProUGUI>(searchRoot, activeElements); FindAndAddActiveTextElements<Text>(searchRoot, activeElements); } return activeElements; }
	private void FindAndAddActiveElements<T>(GameObject searchRoot, HashSet<GameObject> activeSet) where T : Component { /* ... Code remains the same ... */ T[] components; if (searchRoot != null) { components = searchRoot.GetComponentsInChildren<T>(true); } else { components = FindObjectsByType<T>(FindObjectsSortMode.None); } foreach (var comp in components) { if (comp != null && comp.gameObject.activeInHierarchy) { activeSet.Add(comp.gameObject); } } }
	private void FindAndAddActiveTextElements<T>(GameObject searchRoot, HashSet<GameObject> activeSet) where T : Component { /* ... Code remains the same ... */ T[] components; if (searchRoot != null) { components = searchRoot.GetComponentsInChildren<T>(true); } else { components = FindObjectsByType<T>(FindObjectsSortMode.None); } foreach (var comp in components) { if (comp != null && comp.gameObject.activeInHierarchy) { string textContent = ""; if (comp is TextMeshProUGUI tmp) { textContent = tmp.text; } else if (comp is Text txt) { textContent = txt.text; } if (!string.IsNullOrWhiteSpace(textContent)) { activeSet.Add(comp.gameObject); } } } }
	private string GetSpokenNameForButton(Button button) { /* ... Code remains the same ... */ if (button == null) { return "unnamed button"; } if (elementToSpokenNameMap.TryGetValue(button, out string spokenName)) { return spokenName; } Debug.LogWarning($"Button '{button.name}' not found in spoken name map. Determining fallback name."); if (preferElementText) { string buttonText = GetButtonText(button); if (!string.IsNullOrWhiteSpace(buttonText)) { return buttonText; } } return CleanNameForDictionary(button.gameObject.name); }
	private string GetSpokenName(Component component, string fallbackCleanedName) { /* ... Code remains the same ... */ if (component == null) return fallbackCleanedName ?? "unknown element"; if (elementToSpokenNameMap.TryGetValue(component, out string spokenName)) { return spokenName; } return fallbackCleanedName ?? CleanNameForDictionary(component.gameObject.name); }


	// +++ SCREEN READER FUNCTIONALITY (REFACTORED for Sections) +++

	/// <summary>
	/// Describes active UI elements within the default scope, attempting to respect LayoutGroups.
	/// </summary>
	public void DescribeVisibleUI() { /* ... Code remains the same ... */ if (postClickCoroutine != null) { StopCoroutine(postClickCoroutine); postClickCoroutine = null; } GameObject root = targetCanvas?.gameObject; ProcessAndDescribeByHierarchy(root, "visible"); }

	/// <summary>
	/// Describes active UI elements within a specific Canvas identified by its PARENT's name.
	/// </summary>
	public void DescribeCanvasByName(string parentName) { /* ... Code remains the same ... */ if (postClickCoroutine != null) { StopCoroutine(postClickCoroutine); postClickCoroutine = null; } if (!CheckTTSSpeaker()) { return; } string cleanedParentName = CleanDictatedInput(parentName); if (string.IsNullOrWhiteSpace(cleanedParentName)) { SpeakIfAvailable("Please specify a valid UI area name."); return; } Debug.Log($"DescribeCanvasByName: Searching for parent name (cleaned): '{cleanedParentName}' (Original Raw Input: '{parentName}')"); Canvas requestedCanvas = FindObjectsByType<Canvas>(FindObjectsSortMode.None).FirstOrDefault(c => c.transform.parent != null && CleanNameForDictionary(c.transform.parent.gameObject.name).Equals(cleanedParentName) && c.gameObject.activeInHierarchy); if (requestedCanvas == null) { SpeakIfAvailable($"Sorry, I couldn't find an active UI area named {parentName}."); return; } ProcessAndDescribeByHierarchy(requestedCanvas.gameObject, $"in the {parentName} area"); }

	/// <summary>
	/// Describes active UI elements within a specified radius of this GameObject's position, sorted by distance.
	/// </summary>
	public void DescribeNearbyUI(float radius = 2.0f) { /* ... Code remains the same ... */ if (!CheckTTSSpeaker()) { return; } if (postClickCoroutine != null) { StopCoroutine(postClickCoroutine); postClickCoroutine = null; } Debug.Log($"VoiceUI DescribeNearbyUI: Searching for UI elements within {radius}m."); ProcessAndDescribeScopeFlatSort(null, $"within {radius} meters", false, radius); }

	/// <summary>
	/// Determines whether to use hierarchy-based or flat-sorted description.
	/// </summary>
	private void ProcessAndDescribeByHierarchy(GameObject scope, string context) { /* ... Code remains the same ... */ GameObject searchRoot = scope; if (scope == null && targetCanvas != null) { searchRoot = targetCanvas.gameObject; } if (searchRoot == null) { Debug.LogWarning("ProcessAndDescribeByHierarchy: No scope/targetCanvas. Falling back to scene flat sort."); ProcessAndDescribeScopeFlatSort(null, context, true, 0f); return; } bool layoutGroupFound = searchRoot.GetComponentInChildren<LayoutGroup>(true) != null; if (layoutGroupFound) { Debug.Log($"ProcessAndDescribeByHierarchy ({context}): LayoutGroup found. Using recursive traversal."); List<string> descriptionList = new List<string>(); HashSet<GameObject> processedObjects = new HashSet<GameObject>(); ProcessTransformRecursive(searchRoot.transform, descriptionList, processedObjects); StringBuilder description = new StringBuilder(); for (int i = 0; i < descriptionList.Count; i++) { if (i > 0) { description.Append(". "); } description.Append(descriptionList[i]); } SpeakDescriptionResult(description, descriptionList.Count, context); } else { Debug.Log($"ProcessAndDescribeByHierarchy ({context}): No LayoutGroup found. Falling back to Y-sort."); ProcessAndDescribeScopeFlatSort(searchRoot, context, true, 0f); } }


	/// <summary>
	/// Recursive function to traverse the hierarchy, adding formatted descriptions to a list in traversal order.
	/// </summary>
	private void ProcessTransformRecursive(Transform target, List<string> descriptionList, HashSet<GameObject> processedObjects) { /* ... Code remains the same ... */ if (!target.gameObject.activeInHierarchy) { return; } string elementDescription = GetDescriptionForTransform(target, processedObjects); if (!processedObjects.Add(target.gameObject)) { return; } if (!string.IsNullOrEmpty(elementDescription)) { descriptionList.Add(elementDescription); } if (target.childCount > 0) { foreach (Transform child in target) { ProcessTransformRecursive(child, descriptionList, processedObjects); } } }


	/// <summary>
	/// Tries to identify a describable UI element on the target transform and get its info string.
	/// ** UPDATED for Dropdown description logic **
	/// </summary>
	private string GetDescriptionForTransform(Transform target, HashSet<GameObject> processedObjects)
	{
		if (!target.TryGetComponent<RectTransform>(out var rectTransform)) return null;

		string elementType = ""; string nameToSpeak = ""; string status = ""; bool foundElement = false;

		// --- Check for specific UI component types ON THIS TRANSFORM ---

		// 1. Toggle Group
		if (target.TryGetComponent<ToggleGroup>(out var group)) { elementType = "ToggleGroup"; string groupName = CleanNameForDictionary(group.gameObject.name); StringBuilder optionsDesc = new StringBuilder(); var togglesInGroup = new List<Toggle>(); foreach (Transform childTransform in group.transform) { if (childTransform.TryGetComponent<Toggle>(out var childToggle) && childToggle.group == group) { togglesInGroup.Add(childToggle); } } if (togglesInGroup.Count == 0) { togglesInGroup = group.GetComponentsInChildren<Toggle>(true).Where(t => t.group == group).ToList(); } int toggleCount = 0; foreach (var tg in togglesInGroup) { if (tg != null && tg.gameObject.activeInHierarchy) { string toggleLabel = GetToggleLabelText(tg) ?? CleanNameForDictionary(tg.gameObject.name); string state = tg.isOn ? "selected" : "not selected"; if (toggleCount > 0) optionsDesc.Append(", "); optionsDesc.Append($"{toggleLabel} {state}"); processedObjects.Add(tg.gameObject); toggleCount++; } } if (toggleCount > 0) { nameToSpeak = $"{groupName}. Options: {optionsDesc}"; } else { nameToSpeak = $"{groupName}. Contains no active toggle options."; } status = ""; foundElement = true; }
		// 2. Slider
		else if (target.TryGetComponent<Slider>(out var slider)) { elementType = "Slider"; string sliderName = GetSliderLabelText(slider) ?? CleanNameForDictionary(slider.gameObject.name); string valueDesc = slider.wholeNumbers ? $"{slider.value:F0}" : $"{slider.value:F1}"; nameToSpeak = $"{sliderName}, value {valueDesc}, value range {slider.minValue:F0} to {slider.maxValue:F0}"; status = describeInactiveState && !slider.interactable ? "inactive" : ""; foundElement = true; }
		// 3. Input Field (TMP)
		else if (target.TryGetComponent<TMP_InputField>(out var input)) { elementType = "InputField"; string inputName = GetInputFieldLabelText(input) ?? CleanNameForDictionary(input.gameObject.name); string valueText = input.text; if (ignorePlaceholderWhenReading && input.placeholder != null && input.placeholder is TMP_Text placeholderText && !string.IsNullOrEmpty(placeholderText.text)) { if (valueText == placeholderText.text) { valueText = "empty"; } } if (string.IsNullOrWhiteSpace(valueText)) { valueText = "empty"; } status = describeInactiveState && !input.interactable ? "inactive" : ""; nameToSpeak = $"{inputName}, value {valueText}"; foundElement = true; }
		// 4. Dropdown (TMP) (Updated Description Logic)
		else if (target.TryGetComponent<TMP_Dropdown>(out var dropdown))
		{
			elementType = "Dropdown";
			string dropdownName = GetDropdownLabelText(dropdown) ?? CleanNameForDictionary(dropdown.gameObject.name);
			string currentOptionText = "none selected";
			if (dropdown.options != null && dropdown.value >= 0 && dropdown.value < dropdown.options.Count)
			{
				currentOptionText = dropdown.options[dropdown.value].text;
			}

			// Build options list string
			StringBuilder optionListBuilder = new StringBuilder();
			if (dropdown.options != null)
			{
				for (int i = 0; i < dropdown.options.Count; i++)
				{
					if (i > 0) optionListBuilder.Append(", ");
					optionListBuilder.Append(dropdown.options[i].text);
				}
			}
			string optionListString = optionListBuilder.ToString();

			nameToSpeak = $"{dropdownName}, selected {currentOptionText}. The options are {optionListString}.";
			status = describeInactiveState && !dropdown.interactable ? "inactive" : "";
			foundElement = true;
		}
		// 5. Individual Toggle (Checkbox)
		else if (target.TryGetComponent<Toggle>(out var toggle) && toggle.group == null) { if (processedObjects.Contains(toggle.gameObject)) return null; elementType = "Checkbox"; string toggleName = GetToggleLabelText(toggle) ?? CleanNameForDictionary(toggle.gameObject.name); string valueText = toggle.isOn ? "checked" : "unchecked"; status = describeInactiveState && !toggle.interactable ? "inactive" : ""; nameToSpeak = $"{toggleName}, {valueText}"; foundElement = true; }
		// 6. Button
		else if (target.TryGetComponent<Button>(out var button)) { if (processedObjects.Contains(button.gameObject)) return null; elementType = "Button"; nameToSpeak = GetSpokenNameForButton(button); status = (describeInactiveState && !button.IsInteractable()) ? "inactive" : ""; foundElement = true; }
		// 7. Standalone Text (TMP)
		else if (describeStandaloneText && target.TryGetComponent<TextMeshProUGUI>(out var tmp)) { bool isLikelyLabelForNext = false; Transform parent = target.parent; int myIndex = target.GetSiblingIndex(); if (parent != null && myIndex + 1 < parent.childCount) { Transform nextSibling = parent.GetChild(myIndex + 1); if (nextSibling.gameObject.activeInHierarchy) { if (nextSibling.GetComponent<Slider>() != null || nextSibling.GetComponent<TMP_InputField>() != null || nextSibling.GetComponent<TMP_Dropdown>() != null) { isLikelyLabelForNext = true; } } } if (!isLikelyLabelForNext && !string.IsNullOrWhiteSpace(tmp.text)) { if (processedObjects.Contains(tmp.gameObject)) return null; elementType = "Text"; nameToSpeak = tmp.text; status = ""; foundElement = true; } else if (isLikelyLabelForNext) { Debug.Log($"Skipping Text (TMP) on {target.name} as likely label for next control."); } }
		// 8. Standalone Text (Legacy)
		else if (describeStandaloneText && target.TryGetComponent<Text>(out var text)) { bool isLikelyLabelForNext = false; Transform parent = target.parent; int myIndex = target.GetSiblingIndex(); if (parent != null && myIndex + 1 < parent.childCount) { Transform nextSibling = parent.GetChild(myIndex + 1); if (nextSibling.gameObject.activeInHierarchy) { if (nextSibling.GetComponent<Slider>() != null || nextSibling.GetComponent<InputField>() != null || nextSibling.GetComponent<Dropdown>() != null) { isLikelyLabelForNext = true; } } } if (!isLikelyLabelForNext && !string.IsNullOrWhiteSpace(text.text)) { if (processedObjects.Contains(text.gameObject)) return null; elementType = "Text"; nameToSpeak = text.text; status = ""; foundElement = true; } else if (isLikelyLabelForNext) { Debug.Log($"Skipping Text (Legacy) on {target.name} as likely label for next control."); } }

		// Format the final description string using the helper
		if (foundElement)
		{
			UIElementInfo info = new UIElementInfo { RectTransform = rectTransform, Distance = 0, ElementType = elementType, NameToSpeak = nameToSpeak, Status = status };
			StringBuilder tempBuilder = new StringBuilder();
			AppendElementDescriptionFromInfo(tempBuilder, 0, info); // Use helper to format consistently
			return tempBuilder.ToString();
		}
		return null; // No describable element found
	}


	/// <summary>
	/// [Fallback Method] Core logic to find, sort (by Y or distance), and describe various individual UI elements.
	/// </summary>
	private void ProcessAndDescribeScopeFlatSort(GameObject scope, string context, bool sortByY, float radius = 0f) { /* ... Code remains the same ... */ if (!CheckTTSSpeaker()) { return; } List<UIElementInfo> elementsToDescribe = new List<UIElementInfo>(); HashSet<GameObject> processedControlGOs = new HashSet<GameObject>(); Vector3 centerPosition = this.transform.position; float radiusSq = radius * radius; GameObject searchRoot = scope; if (scope == null && targetCanvas != null) { searchRoot = targetCanvas.gameObject; } ToggleGroup[] toggleGroups = FindObjectsByType<ToggleGroup>(FindObjectsSortMode.None); foreach (var group in toggleGroups) { if (searchRoot != null && !group.transform.IsChildOf(searchRoot.transform)) continue; if (group != null && group.gameObject.activeInHierarchy && group.TryGetComponent<RectTransform>(out var groupRect)) { if (radius > 0 && Vector3.SqrMagnitude(groupRect.position - centerPosition) > radiusSq) continue; string desc = GetDescriptionForTransform(group.transform, processedControlGOs); if (desc != null) { float dist = (radius > 0) ? Vector3.Distance(centerPosition, groupRect.position) : 0f; elementsToDescribe.Add(new UIElementInfo { RectTransform = groupRect, Distance = dist, ElementType = "ToggleGroup", NameToSpeak = desc, Status = "" }); processedControlGOs.Add(group.gameObject); } } } Slider[] sliders = FindObjectsByType<Slider>(FindObjectsSortMode.None); foreach (var slider in sliders) { if (searchRoot != null && !slider.transform.IsChildOf(searchRoot.transform)) continue; if (slider != null && slider.gameObject.activeInHierarchy && slider.TryGetComponent<RectTransform>(out var sliderRect)) { if (processedControlGOs.Contains(slider.gameObject)) continue; if (radius > 0 && Vector3.SqrMagnitude(sliderRect.position - centerPosition) > radiusSq) continue; string desc = GetDescriptionForTransform(slider.transform, processedControlGOs); if (desc != null) { float dist = (radius > 0) ? Vector3.Distance(centerPosition, sliderRect.position) : 0f; string status = describeInactiveState && !slider.interactable ? "inactive" : ""; elementsToDescribe.Add(new UIElementInfo { RectTransform = sliderRect, Distance = dist, ElementType = "Slider", NameToSpeak = desc, Status = status }); processedControlGOs.Add(slider.gameObject); } } } TMP_InputField[] inputFields = FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None); foreach (var input in inputFields) { if (searchRoot != null && !input.transform.IsChildOf(searchRoot.transform)) continue; if (input != null && input.gameObject.activeInHierarchy && input.TryGetComponent<RectTransform>(out var inputRect)) { if (processedControlGOs.Contains(input.gameObject)) continue; if (radius > 0 && Vector3.SqrMagnitude(inputRect.position - centerPosition) > radiusSq) continue; string desc = GetDescriptionForTransform(input.transform, processedControlGOs); if (desc != null) { float dist = (radius > 0) ? Vector3.Distance(centerPosition, inputRect.position) : 0f; string status = describeInactiveState && !input.interactable ? "inactive" : ""; elementsToDescribe.Add(new UIElementInfo { RectTransform = inputRect, Distance = dist, ElementType = "InputField", NameToSpeak = desc, Status = status }); processedControlGOs.Add(input.gameObject); } } } TMP_Dropdown[] dropdowns = FindObjectsByType<TMP_Dropdown>(FindObjectsSortMode.None); foreach (var dropdown in dropdowns) { if (searchRoot != null && !dropdown.transform.IsChildOf(searchRoot.transform)) continue; if (dropdown != null && dropdown.gameObject.activeInHierarchy && dropdown.TryGetComponent<RectTransform>(out var dropdownRect)) { if (processedControlGOs.Contains(dropdown.gameObject)) continue; if (radius > 0 && Vector3.SqrMagnitude(dropdownRect.position - centerPosition) > radiusSq) continue; string desc = GetDescriptionForTransform(dropdown.transform, processedControlGOs); if (desc != null) { float dist = (radius > 0) ? Vector3.Distance(centerPosition, dropdownRect.position) : 0f; string status = describeInactiveState && !dropdown.interactable ? "inactive" : ""; elementsToDescribe.Add(new UIElementInfo { RectTransform = dropdownRect, Distance = dist, ElementType = "Dropdown", NameToSpeak = desc, Status = status }); processedControlGOs.Add(dropdown.gameObject); } } } Toggle[] toggles = FindObjectsByType<Toggle>(FindObjectsSortMode.None); foreach (var toggle in toggles) { if (searchRoot != null && !toggle.transform.IsChildOf(searchRoot.transform)) continue; if (toggle != null && toggle.gameObject.activeInHierarchy && toggle.TryGetComponent<RectTransform>(out var toggleRect)) { if (processedControlGOs.Contains(toggle.gameObject)) continue; if (toggle.group != null) continue; if (radius > 0 && Vector3.SqrMagnitude(toggleRect.position - centerPosition) > radiusSq) continue; string desc = GetDescriptionForTransform(toggle.transform, processedControlGOs); if (desc != null) { float dist = (radius > 0) ? Vector3.Distance(centerPosition, toggleRect.position) : 0f; string status = describeInactiveState && !toggle.interactable ? "inactive" : ""; elementsToDescribe.Add(new UIElementInfo { RectTransform = toggleRect, Distance = dist, ElementType = "Checkbox", NameToSpeak = desc, Status = status }); processedControlGOs.Add(toggle.gameObject); } } } Button[] buttons = FindObjectsByType<Button>(FindObjectsSortMode.None); foreach (var button in buttons) { if (searchRoot != null && !button.transform.IsChildOf(searchRoot.transform)) continue; if (button != null && button.gameObject.activeInHierarchy && button.TryGetComponent<RectTransform>(out var buttonRect)) { if (processedControlGOs.Contains(button.gameObject)) continue; if (radius > 0 && Vector3.SqrMagnitude(buttonRect.position - centerPosition) > radiusSq) continue; string desc = GetDescriptionForTransform(button.transform, processedControlGOs); if (desc != null) { float dist = (radius > 0) ? Vector3.Distance(centerPosition, buttonRect.position) : 0f; string status = (describeInactiveState && !button.IsInteractable()) ? "inactive" : ""; elementsToDescribe.Add(new UIElementInfo { RectTransform = buttonRect, Distance = dist, ElementType = "Button", NameToSpeak = desc, Status = status }); processedControlGOs.Add(button.gameObject); } } } if (describeStandaloneText) { TextMeshProUGUI[] textComponentsTMP = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None); foreach (var tmp in textComponentsTMP) { if (searchRoot != null && !tmp.transform.IsChildOf(searchRoot.transform)) continue; if (tmp != null && tmp.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(tmp.text) && tmp.TryGetComponent<RectTransform>(out var textRect)) { bool parentIsControl = tmp.transform.parent != null && (tmp.transform.parent.GetComponent<Selectable>() != null); if (processedControlGOs.Contains(tmp.gameObject) || parentIsControl) continue; if (radius > 0 && Vector3.SqrMagnitude(textRect.position - centerPosition) > radiusSq) continue; string desc = GetDescriptionForTransform(tmp.transform, processedControlGOs); if (desc != null) { float dist = (radius > 0) ? Vector3.Distance(centerPosition, textRect.position) : 0f; elementsToDescribe.Add(new UIElementInfo { RectTransform = textRect, Distance = dist, ElementType = "Text", NameToSpeak = desc, Status = "" }); } } } Text[] textComponentsLegacy = FindObjectsByType<Text>(FindObjectsSortMode.None); foreach (var text in textComponentsLegacy) { if (searchRoot != null && !text.transform.IsChildOf(searchRoot.transform)) continue; if (text != null && text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(text.text) && text.TryGetComponent<RectTransform>(out var textRect)) { bool parentIsControl = text.transform.parent != null && (text.transform.parent.GetComponent<Selectable>() != null); if (processedControlGOs.Contains(text.gameObject) || parentIsControl) continue; if (radius > 0 && Vector3.SqrMagnitude(textRect.position - centerPosition) > radiusSq) continue; string desc = GetDescriptionForTransform(text.transform, processedControlGOs); if (desc != null) { float dist = (radius > 0) ? Vector3.Distance(centerPosition, textRect.position) : 0f; elementsToDescribe.Add(new UIElementInfo { RectTransform = textRect, Distance = dist, ElementType = "Text", NameToSpeak = desc, Status = "" }); } } } } List<UIElementInfo> sortedElements; if (sortByY) { sortedElements = elementsToDescribe.OrderByDescending(info => info.RectTransform.position.y).ToList(); } else { sortedElements = elementsToDescribe.OrderBy(info => info.Distance).ToList(); } StringBuilder description = new StringBuilder(); for (int i = 0; i < sortedElements.Count; i++) { AppendElementDescriptionFromInfo(description, i, sortedElements[i]); } SpeakDescriptionResult(description, sortedElements.Count, context); }

	/// <summary>
	/// Helper to append a single element's description to the StringBuilder using UIElementInfo.
	/// </summary>
	private void AppendElementDescriptionFromInfo(StringBuilder builder, int index, UIElementInfo elementInfo) { /* ... Code remains the same ... */ if (index > 0) { builder.Append(". "); } string statusPrefix = !string.IsNullOrEmpty(elementInfo.Status) ? elementInfo.Status + " " : ""; string typePrefix = ""; switch (elementInfo.ElementType) { case "Button": case "Slider": case "Checkbox": case "ToggleGroup": case "InputField": case "Dropdown": typePrefix = elementInfo.ElementType + " "; break; default: typePrefix = ""; break; } string sanitizedNameToSpeak = elementInfo.NameToSpeak?.Replace("\n", " ").Replace("\r", "") ?? ""; builder.Append($"{statusPrefix}{typePrefix}{sanitizedNameToSpeak}"); }

	/// <summary>
	/// Helper to speak the final description result, chunking if necessary.
	/// </summary>
	private void SpeakDescriptionResult(StringBuilder builder, int elementCount, string context) { /* ... Code remains the same ... */ if (elementCount <= 0) { SpeakIfAvailable($"There are no recognized UI elements {context} right now."); return; } string fullDescription = builder.ToString(); if (string.IsNullOrWhiteSpace(fullDescription)) { SpeakIfAvailable($"There are no recognized UI elements {context} right now."); return; } if (fullDescription.Length <= ttsMaxChunkLength) { SpeakIfAvailable(fullDescription); } else { Debug.Log($"SpeakDescriptionResult: Description length ({fullDescription.Length}) exceeds max chunk length ({ttsMaxChunkLength}). Chunking..."); string[] sentences = fullDescription.Split(new[] { ". " }, StringSplitOptions.RemoveEmptyEntries); StringBuilder chunkBuilder = new StringBuilder(); for (int i = 0; i < sentences.Length; i++) { string currentSentence = sentences[i]; if (chunkBuilder.Length > 0 && chunkBuilder.Length + currentSentence.Length + 2 > ttsMaxChunkLength) { SpeakIfAvailable(chunkBuilder.ToString().Trim()); chunkBuilder.Clear(); } if (chunkBuilder.Length > 0) { chunkBuilder.Append(". "); } chunkBuilder.Append(currentSentence); if (i == sentences.Length - 1) { SpeakIfAvailable(chunkBuilder.ToString().Trim()); } } Debug.Log($"SpeakDescriptionResult: Finished queueing chunks."); } }

	/// <summary>
	/// Helper method to queue text for speaking via the TTSSpeaker. (Removed Stop())
	/// </summary>
	private void SpeakIfAvailable(string textToSpeak) { /* ... Code remains the same ... */ if (ttsSpeaker != null && !string.IsNullOrEmpty(textToSpeak)) { ttsSpeaker.SpeakQueued(textToSpeak); } else if (ttsSpeaker == null) { Debug.LogError("VoiceUIController: TTSSpeaker not assigned."); } }

	/// <summary>
	/// Helper to check if TTSSpeaker is assigned.
	/// </summary>
	private bool CheckTTSSpeaker() { /* ... Code remains the same ... */ if (ttsSpeaker != null) { return true; } Debug.LogError("VoiceUIController: TTSSpeaker is not assigned."); return false; }

	/// <summary>
	/// Call this method to update the internal mapping of interactable elements.
	/// </summary>
	public void RefreshAllInteractableElements() { /* ... Code remains the same ... */ Debug.Log("VoiceUIController: Refreshing interactable element dictionaries."); MapAllInteractableElements(); }
}