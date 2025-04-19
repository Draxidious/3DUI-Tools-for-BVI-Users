using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Oculus.Voice.Dictation;
using UnityEngine.InputSystem; // Adjust this if your SDK version uses a different namespace

// This script name is DictationActivationObjectInteraction.
public class DictationActivationObjectInteraction : MonoBehaviour
{
    [Header("References")]
    // Reference to the BVIColliderManager to call announcement methods.
    public BVIColliderManager colliderManager;

    // Reference to the Meta App Dictation building block.
    public AppDictationExperience dictationExperience;

    // Reference to the InputData script that manages XR input devices.
    public InputData inputData;

    [Header("Audio")]
    public AudioClip dictationStartSound;
    public AudioClip dictationStopSound;

    private AudioSource audioSource;
    private bool isListening = false;

    // Dictionary mapping keywords (and synonyms) to actions.
    private Dictionary<string, Action> keywordActions;

    private void Awake()
    {
        // Ensure an AudioSource is attached.
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            Debug.LogWarning("No AudioSource component found. Please attach an AudioSource to play dictation sounds.");
        }

        // Build the dictionary mapping words to functions.
        // "forward" is triggered by "forward", "front", "ahead"
        // "backward" is triggered by "backward", "back", "behind"
        keywordActions = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase)
        {
            { "forward",  () => colliderManager?.AnnounceForwardDescription() },
            { "front",    () => colliderManager?.AnnounceForwardDescription() },
            { "ahead",    () => colliderManager?.AnnounceForwardDescription() },

            { "backward", () => colliderManager?.AnnounceBackwardDescription() },
            { "back",     () => colliderManager?.AnnounceBackwardDescription() },
            { "behind",   () => colliderManager?.AnnounceBackwardDescription() },

            { "left",     () => colliderManager?.AnnounceLeftDescription() },
            { "right",    () => colliderManager?.AnnounceRightDescription() }
        };

        // Register to App Dictation events.
        if (dictationExperience != null && dictationExperience.DictationEvents != null)
        {
            dictationExperience.DictationEvents.OnStartListening.AddListener(OnStartListening);
            dictationExperience.DictationEvents.OnStoppedListening.AddListener(OnStoppedListening);
            dictationExperience.DictationEvents.OnFullTranscription.AddListener(OnFinalTranscriptionReceived);
        }
        else
        {
            Debug.LogWarning("AppDictationExperience or its DictationEvents not assigned.");
        }
    }

    private void OnDestroy()
    {
        // Unregister listeners to prevent memory leaks.
        if (dictationExperience != null && dictationExperience.DictationEvents != null)
        {
            dictationExperience.DictationEvents.OnStartListening.RemoveListener(OnStartListening);
            dictationExperience.DictationEvents.OnStoppedListening.RemoveListener(OnStoppedListening);
            dictationExperience.DictationEvents.OnFullTranscription.RemoveListener(OnFinalTranscriptionReceived);
        }
    }

    private void Update()
    {
        // Use the provided InputData to check for a button press on the right controller.
        // This example uses the menuButton via UnityEngine.XR.CommonUsages.
        if (!isListening &&
            inputData._leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool value) && value)
        {
            // Activate dictation when the button is pressed.
            dictationExperience.Activate();
            isListening = true;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            // Activate dictation when the button is pressed.
            dictationExperience.Activate();
            isListening = true;
        }
    }

    // Called when dictation starts listening (microphone opens).
    private void OnStartListening()
    {
        PlaySound(dictationStartSound);
        Debug.Log("[DictationActivationObjectInteraction] Dictation started listening.");
    }

    // Called when dictation stops listening (microphone closes).
    private void OnStoppedListening()
    {
        PlaySound(dictationStopSound);
        Debug.Log("[DictationActivationObjectInteraction] Dictation stopped listening.");
    }

    // Called when the dictation system returns a final transcription.
    private void OnFinalTranscriptionReceived(string transcription)
    {
        Debug.Log("[DictationActivationObjectInteraction] Final transcription: " + transcription);

        // Stop dictation for this session.
        dictationExperience.Deactivate();

        // Process the transcription to find the first valid keyword.
        ProcessTranscription(transcription);

        isListening = false;
    }

    /// <summary>
    /// Scans the transcription word by word and invokes the action corresponding to
    /// the first keyword (or synonym) encountered.
    /// </summary>
    /// <param name="transcription">The final transcription string.</param>
    private void ProcessTranscription(string transcription)
    {
        if (string.IsNullOrEmpty(transcription))
        {
            Debug.LogWarning("[DictationActivationObjectInteraction] Empty transcription received.");
            return;
        }

        // Split the transcription by whitespace into individual words.
        string[] words = transcription.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        // Iterate over each word and check if it is a valid keyword.
        foreach (string word in words)
        {
            if (keywordActions.TryGetValue(word, out Action action))
            {
                action.Invoke();
                Debug.Log($"[DictationActivationObjectInteraction] Keyword \"{word}\" processed.");
                break; // Stop after the first keyword is processed.
            }
        }
    }

    /// <summary>
    /// Utility method to play an AudioClip via the attached AudioSource.
    /// </summary>
    /// <param name="clip">The AudioClip to play.</param>
    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
