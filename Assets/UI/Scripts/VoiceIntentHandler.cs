//using UnityEngine;
//using Meta.WitAi; // Core Wit namespace
//using Meta.WitAi.CallbackHandlers; // For response handlers if needed, but we'll use events
//using Meta.WitAi.Data; // For WitResponseNode
//using Meta.WitAi.Requests; // For WitRequest events
//using System.Linq;
//using Oculus.Voice;
//using Meta.WitAi.Json; // For LINQ operations if needed (like FirstOrDefault)

//public class VoiceIntentHandler : MonoBehaviour
//{
//	[Header("Voice SDK Reference")]
//	[Tooltip("Drag your AppVoiceExperience GameObject or Component here.")]
//	[SerializeField] private AppVoiceExperience appVoiceExperience;

//	[Header("Intent Configuration")]
//	[Tooltip("The exact name of the first intent configured in Wit.ai.")]
//	[SerializeField] private string intentNameOne = "YourIntentNameOne"; // <-- REPLACE THIS

//	[Tooltip("The exact name of the second intent configured in Wit.ai.")]
//	[SerializeField] private string intentNameTwo = "YourIntentNameTwo"; // <-- REPLACE THIS

//	[Header("Confidence Threshold")]
//	[Tooltip("Minimum confidence score (0.0 to 1.0) required to trigger the intent.")]
//	[Range(0f, 1f)]
//	[SerializeField] private float minimumConfidence = 0.8f;

//	// --- Unity Lifecycle Methods ---

//	void OnEnable()
//	{
//		// Ensure the AppVoiceExperience reference is set
//		if (appVoiceExperience == null)
//		{
//			Debug.LogError($"'{nameof(AppVoiceExperience)}' reference not set on {gameObject.name}. Please assign it in the Inspector.");
//			this.enabled = false; // Disable this script if setup is invalid
//			return;
//		}

//		// Subscribe to the Wit.ai response event
//		// This event fires when Wit.ai successfully processes the audio and returns data
//		appVoiceExperience.VoiceEvents.OnResponse.AddListener(HandleWitResponse);

//		// Optional: Subscribe to error events for debugging
//		appVoiceExperience.VoiceEvents.OnError.AddListener(HandleWitError);

//		Debug.Log("VoiceIntentHandler enabled and subscribed to Wit.ai events.");
//	}

//	void OnDisable()
//	{
//		// Unsubscribe when the object is disabled or destroyed to prevent memory leaks
//		if (appVoiceExperience != null)
//		{
//			appVoiceExperience.VoiceEvents.OnResponse.RemoveListener(HandleWitResponse);
//			appVoiceExperience.VoiceEvents.OnError.RemoveListener(HandleWitError);
//			Debug.Log("VoiceIntentHandler disabled and unsubscribed from Wit.ai events.");
//		}
//	}

//	// --- Wit.ai Event Handlers ---

//	/// <summary>
//	/// Handles the response received from Wit.ai after voice input processing.
//	/// </summary>
//	/// <param name="response">The data structure containing the NLU results (intents, entities, etc.)</param>
//	private void HandleWitResponse(WitResponseNode response)
//	{
//		if (response == null)
//		{
//			Debug.LogWarning("Received a null WitResponseNode.");
//			return;
//		}

//		// Wit.ai can return multiple intents; usually, the first one is the most likely.
//		// Use GetIntentNode() which often gets the highest confidence intent automatically.
//		WitIntentNode intentNode = response.GetIntentNode();

//		if (intentNode == null)
//		{
//			// It's possible Wit didn't recognize any intent confidently
//			// You might get transcription results here though: Debug.Log($"Transcription: {response.GetTranscription()}");
//			return;
//		}

//		string recognizedIntentName = intentNode.Name; // Get the intent name (e.g., "YourIntentNameOne")
//		float confidence = intentNode.Confidence;      // Get the confidence score (0.0 to 1.0)

//		Debug.Log($"Wit.ai Response: Intent='{recognizedIntentName}', Confidence={confidence:F2}");

//		// Check if the confidence meets our threshold
//		if (confidence < minimumConfidence)
//		{
//			Debug.Log($"Intent '{recognizedIntentName}' ignored due to low confidence ({confidence:F2} < {minimumConfidence}).");
//			return;
//		}

//		// --- Intent Matching and Function Triggering ---

//		// Compare the recognized intent name with our target intents (case-insensitive)
//		if (string.Equals(recognizedIntentName, intentNameOne, System.StringComparison.OrdinalIgnoreCase))
//		{
//			Debug.Log($"Executing action for Intent: {intentNameOne}");
//			TriggerActionForIntentOne(response); // Pass the response for potential entity extraction
//		}
//		else if (string.Equals(recognizedIntentName, intentNameTwo, System.StringComparison.OrdinalIgnoreCase))
//		{
//			Debug.Log($"Executing action for Intent: {intentNameTwo}");
//			TriggerActionForIntentTwo(response); // Pass the response
//		}
//		else
//		{
//			// Optional: Handle cases where a different intent was recognized above the threshold
//			Debug.Log($"Recognized intent '{recognizedIntentName}' does not match the configured intents in this script.");
//		}
//	}

//	/// <summary>
//	/// Handles errors reported by the Voice SDK.
//	/// </summary>
//	private void HandleWitError(string errorTitle, string errorMessage)
//	{
//		Debug.LogError($"Voice SDK Error: [{errorTitle}] {errorMessage}");
//	}


//	// --- Your Custom Functions to Trigger ---

//	/// <summary>
//	/// This function is called when 'intentNameOne' is recognized with sufficient confidence.
//	/// </summary>
//	/// <param name="response">The full Wit response, useful for extracting entities.</param>
//	private void TriggerActionForIntentOne(WitResponseNode response)
//	{
//		Debug.Log("ACTION: TriggerActionForIntentOne() Executed!");
//		// TODO: Add your specific logic for the first intent here.
//		// Example: Change a light color, move an object, play a sound effect.

//		// Example of getting an entity value:
//		// string colorValue = response.GetEntityValue("color:color"); // Assumes you have a 'color' entity
//		// if (!string.IsNullOrEmpty(colorValue)) {
//		//    Debug.Log($"Detected color entity: {colorValue}");
//		//    // ChangeLightColor(colorValue);
//		// }
//	}

//	/// <summary>
//	/// This function is called when 'intentNameTwo' is recognized with sufficient confidence.
//	/// </summary>
//	/// <param name="response">The full Wit response, useful for extracting entities.</param>
//	private void TriggerActionForIntentTwo(WitResponseNode response)
//	{
//		Debug.Log("ACTION: TriggerActionForIntentTwo() Executed!");
//		// TODO: Add your specific logic for the second intent here.
//		// Example: Start a mini-game, show information, trigger an animation.

//		// Example of getting an entity value:
//		// string objectName = response.GetEntityValue("object:object"); // Assumes 'object' entity
//		// if (!string.IsNullOrEmpty(objectName)) {
//		//    Debug.Log($"Detected object entity: {objectName}");
//		//    // FindAndInteractWithObject(objectName);
//		// }
//	}
//}